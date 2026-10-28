using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GRW_DKRP_Panel;

/// <summary>Engine independente do Cheat Engine. Implementa somente os recursos portados e validados pela tabela.</summary>
internal sealed class WildlandsTrainer : IDisposable
{
    private const string PlayerSignature = "40 53 48 83 EC ?? 0F 29 74 24 20 48 89 CB 0F 28 F1 E8 ?? ?? ?? ?? 80 BB ?? ?? ?? ?? 00 74 ?? 48";
    private const string MarkerSignature = "C6 83 A0 00 00 00 05 E8 ?? ?? ?? ?? 48 83 C4 40 5B C3";
    private const int MarkerReadyOffset = 0xA0, MarkerCoordsOffset = 0x90, PlayerEntityOffset = 0x10, PlayerCoordsOffset = 0x50;
    private readonly ProcessMemory _memory;
    private readonly ulong _data;
    private readonly ulong _componentSlot, _healthSlot, _vehicleSlot, _markerSlot;
    private readonly HashSet<string> _enabled = new(StringComparer.Ordinal);
    private DateTime _lastTick = DateTime.UtcNow;
    private (ulong Entity, float X, float Y, float Z)? _lastPosition;
    private float? _noclipZ;
    private bool _spaceWasDown;
    private bool _disposed;

    public event Action<string>? Log;
    public int ProcessId => _memory.ProcessId;
    public bool IsReady { get; private set; }

    public WildlandsTrainer(int pid)
    {
        _memory = new ProcessMemory(pid);
        try
        {
            // Resolve every signature before the first patch so a mismatch leaves the game untouched.
            var playerSite = _memory.FindUnique(PlayerSignature);
            var markerSite = _memory.FindUnique(MarkerSignature);
            var playerPrefix = _memory.Read(playerSite, 6);
            var markerPrefix = _memory.Read(markerSite, 7);
            byte[] markerExpected = [0xC6, 0x83, 0xA0, 0, 0, 0, 0x05];
            if (!markerPrefix.AsSpan().SequenceEqual(markerExpected)) throw new InvalidOperationException("O marcador não corresponde à assinatura validada; nada foi alterado.");

            _data = _memory.AllocateNear(playerSite, 0x1000);
            _componentSlot = _data;
            _healthSlot = _data + 8;
            _vehicleSlot = _data + 16;
            _markerSlot = _data + 24;
            _memory.Write(_data, new byte[32]);
            var playerHook = BuildPlayerCapture(_componentSlot, _healthSlot, _vehicleSlot);
            var markerHook = BuildMarkerCapture(_markerSlot);
            _memory.Hook("captura-jogador", playerSite, playerPrefix, playerHook);
            _memory.Hook("captura-marcador", markerSite, markerPrefix, markerHook);
            IsReady = true;
            Log?.Invoke("Assinaturas únicas; captura local instalada. Mova o personagem e crie um marcador novo.");
        }
        catch
        {
            _memory.Dispose();
            throw;
        }
    }

    public void SetEnabled(string feature, bool enabled)
    {
        ThrowIfDisposed();
        if (enabled == _enabled.Contains(feature)) return;
        switch (feature)
        {
            case "god": SetGod(enabled); break;
            case "stealth": SetStealth(enabled); break;
            case "speed": SetMotion("speed", enabled); break;
            case "jump": SetMotion("jump", enabled); break;
            case "noclip": SetMotion("noclip", enabled); break;
            case "teleport":
                if (enabled) Log?.Invoke("Teleporte pronto: coloque um marcador novo e use NUMPAD 0.");
                else Log?.Invoke("Atalho de teleporte desativado.");
                if (enabled) _enabled.Add(feature); else _enabled.Remove(feature);
                return;
            default: throw new NotSupportedException("Esse recurso não foi portado para o painel independente.");
        }
        if (enabled) _enabled.Add(feature); else _enabled.Remove(feature);
        Log?.Invoke($"Recurso {feature}: {(enabled ? "ativado" : "desativado")}.");
    }

    private void SetGod(bool enabled)
    {
        var health = Pointer(_healthSlot);
        if (!enabled) { _memory.Restore("god"); return; }
        if (health == 0) throw new InvalidOperationException("Volte ao personagem e mova-o para capturar a estrutura de saúde.");
        for (var i = 0; i < 3; i++)
        {
            var address = health + 0x201UL + (uint)i;
            if (!_enabled.Contains("god")) _memory.WriteTracked("god", address, [1]);
            else if (_memory.ReadByte(address) != 1) _memory.Write(address, [1]);
        }
    }

    private void SetStealth(bool enabled)
    {
        var address = _memory.ModuleBase + 0x13BEAC48UL;
        byte[] original = [0xF3, 0x45, 0x0F, 0x59, 0xC1];
        byte[] patch = [0x45, 0x0F, 0x57, 0xC0, 0x90];
        if (enabled) _memory.Patch("stealth", address, original, patch);
        else _memory.Restore("stealth");
    }

    private void SetMotion(string mode, bool enabled)
    {
        if (mode == "noclip") _noclipZ = null;
        if (!enabled) _lastPosition = null;
    }

    public void Tick()
    {
        if (_disposed || !IsReady) return;
        try
        {
            if (_enabled.Contains("god")) SetGod(true);
            if (!_enabled.Overlaps(["speed", "jump", "noclip"])) return;
            if (!GameIsForeground(ProcessId)) { _lastPosition = null; return; }
            var component = Pointer(_componentSlot);
            var entity = component == 0 ? 0 : _memory.ReadUInt64(component + PlayerEntityOffset);
            if (entity == 0) { _lastPosition = null; return; }
            var pos = entity + PlayerCoordsOffset;
            var w = _memory.ReadFloat(pos + 12);
            if (!float.IsFinite(w) || Math.Abs(w - 1f) > 0.001f) { _lastPosition = null; return; }
            var x = _memory.ReadFloat(pos); var y = _memory.ReadFloat(pos + 4); var z = _memory.ReadFloat(pos + 8);
            if (!Valid(x, y, z)) { _lastPosition = null; return; }
            var now = DateTime.UtcNow;
            var dt = (float)Math.Clamp((now - _lastTick).TotalSeconds, 0, 0.1);
            _lastTick = now;
            if (_enabled.Contains("noclip"))
            {
                _noclipZ ??= z;
                var fx = _memory.ReadFloat(entity + 0x30); var fy = _memory.ReadFloat(entity + 0x34);
                var norm = Math.Sqrt(fx * fx + fy * fy);
                if (!double.IsFinite(norm) || norm < 0.01 || norm > 2) { fx = 0; fy = 1; norm = 1; }
                fx /= (float)norm; fy /= (float)norm;
                var forward = (KeyDown(0x57) ? 1 : 0) - (KeyDown(0x53) ? 1 : 0);
                var strafe = (KeyDown(0x44) ? 1 : 0) - (KeyDown(0x41) ? 1 : 0);
                var rate = 12f * dt;
                x += (fx * forward + fy * strafe) * rate;
                y += (fy * forward - fx * strafe) * rate;
                _noclipZ += ((KeyDown(0x20) ? 1 : 0) - (KeyDown(0x11) ? 1 : 0)) * rate;
                z = _noclipZ.Value;
                WritePosition(pos, x, y, z);
            }
            else
            {
                if (_enabled.Contains("speed") && _lastPosition is { } last && last.Entity == entity &&
                    (KeyDown(0x57) || KeyDown(0x53) || KeyDown(0x41) || KeyDown(0x44)))
                {
                    var dx = x - last.X; var dy = y - last.Y;
                    if (dx * dx + dy * dy < 16f) { x += dx * 2f; y += dy * 2f; WritePosition(pos, x, y, z); }
                }
                if (_enabled.Contains("jump"))
                {
                    var space = KeyDown(0x20);
                    if (space && !_spaceWasDown) { z += 6f; WritePosition(pos, x, y, z); }
                    _spaceWasDown = space;
                }
            }
            _lastPosition = (entity, x, y, z);
        }
        catch (Exception ex) { Log?.Invoke("Tick interrompido: " + ex.Message); }
    }

    public async Task TeleportToMarkerAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var component = Pointer(_componentSlot);
        var marker = Pointer(_markerSlot);
        if (component == 0) throw new InvalidOperationException("Mova o personagem para capturá-lo antes de teleportar.");
        if (marker == 0) throw new InvalidOperationException("Crie um marcador novo no mapa depois de ativar o painel.");
        var state = _memory.ReadByte(marker + MarkerReadyOffset);
        if (state is not (1 or 5)) throw new InvalidOperationException("O marcador ainda não está pronto. Feche o mapa e tente um ponto novo.");
        var entity = _memory.ReadUInt64(component + PlayerEntityOffset);
        if (entity == 0) throw new InvalidOperationException("Ponteiro do jogador inválido.");
        var pos = entity + PlayerCoordsOffset;
        if (Math.Abs(_memory.ReadFloat(pos + 12) - 1f) > 0.001f) throw new InvalidOperationException("A estrutura de posição mudou; teleporte cancelado.");
        var origin = new[] { _memory.ReadFloat(pos), _memory.ReadFloat(pos + 4), _memory.ReadFloat(pos + 8) };
        var target = new[] { _memory.ReadFloat(marker + MarkerCoordsOffset), _memory.ReadFloat(marker + MarkerCoordsOffset + 4), _memory.ReadFloat(marker + MarkerCoordsOffset + 8) };
        if (!Valid([.. origin]) || !Valid([.. target])) throw new InvalidOperationException("Coordenadas inválidas.");
        var final = new[] { target[0], target[1], target[2] + 1.5f };
        var dx = final[0] - origin[0]; var dy = final[1] - origin[1]; var dz = final[2] - origin[2];
        var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        var steps = Math.Max(1, (int)Math.Ceiling(distance / 75.0));
        Log?.Invoke($"Teleporte: carregando rota de {distance:0} m em {steps} etapas.");
        try
        {
            for (var i = 1; i <= steps; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Process.GetProcessById(ProcessId).HasExited) throw new InvalidOperationException("GRW.exe encerrou durante o teleporte.");
                if (Pointer(_markerSlot) != marker) throw new InvalidOperationException("O marcador mudou durante o carregamento.");
                var t = (float)i / steps;
                WritePosition(pos, origin[0] + dx * t, origin[1] + dy * t, origin[2] + dz * t);
                await Task.Delay(120, cancellationToken).ConfigureAwait(false);
            }
            await Task.Delay(650, cancellationToken).ConfigureAwait(false);
            var actualX = _memory.ReadFloat(pos); var actualY = _memory.ReadFloat(pos + 4); var actualZ = _memory.ReadFloat(pos + 8);
            if ((actualX - final[0]) * (actualX - final[0]) + (actualY - final[1]) * (actualY - final[1]) > 625 || Math.Abs(actualZ - final[2]) > 100)
                throw new InvalidOperationException("O jogo não estabilizou na área do marcador; reposicione o marcador em terreno aberto.");
            Log?.Invoke("Teleporte concluído; posição estabilizada próxima ao marcador.");
        }
        catch (Exception ex)
        {
            try { WritePosition(pos, origin[0], origin[1], origin[2]); Log?.Invoke("Rota interrompida; posição de origem restaurada."); }
            catch { Log?.Invoke("Rota interrompida e não foi possível restaurar a origem."); }
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    private ulong Pointer(ulong address) { var value = _memory.ReadUInt64(address); return value > 0x10000 && value < 0x800000000000 ? value : 0; }
    private static bool Valid(params float[] v) => v.All(n => float.IsFinite(n) && Math.Abs(n) < 1_000_000);
    private void WritePosition(ulong address, float x, float y, float z)
    {
        if (!Valid(x, y, z)) throw new InvalidOperationException("A coordenada excede o intervalo aceito.");
        _memory.WriteFloat(address, x); _memory.WriteFloat(address + 4, y); _memory.WriteFloat(address + 8, z);
    }
    private static byte[] BuildPlayerCapture(ulong component, ulong health, ulong vehicle)
    {
        var bytes = new List<byte> { 0x50, 0x41, 0x53 }; // push rax, push r11
        StoreRegister(bytes, component, 0x1B); // mov [r11],rbx
        StoreRegister(bytes, health, 0x0B);    // mov [r11],rcx
        bytes.AddRange([0x48, 0x8B, 0x41, 0x30]); // mov rax,[rcx+30]
        StoreRegister(bytes, vehicle, 0x03);   // mov [r11],rax
        bytes.AddRange([0x41, 0x5B, 0x58]); // pop r11, pop rax
        return [.. bytes];
    }
    private static byte[] BuildMarkerCapture(ulong marker)
    {
        var bytes = new List<byte> { 0x41, 0x53 };
        StoreRegister(bytes, marker, 0x1B);
        bytes.Add(0x41); bytes.Add(0x5B);
        return [.. bytes];
    }
    private static void StoreRegister(List<byte> bytes, ulong address, byte modRm)
    {
        bytes.AddRange([0x49, 0xBB]);
        bytes.AddRange(BitConverter.GetBytes(address));
        bytes.AddRange([0x49, 0x89, modRm]);
    }
    private static bool KeyDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    private static bool GameIsForeground(int pid)
    {
        var window = GetForegroundWindow();
        GetWindowThreadProcessId(window, out var foregroundPid);
        return foregroundPid == pid;
    }
    private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(WildlandsTrainer)); }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _memory.Dispose();
    }

    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);
}
