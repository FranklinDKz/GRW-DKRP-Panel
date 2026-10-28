using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using Iced.Intel;

namespace GRW_DKRP_Panel;

/// <summary>Leitura/escrita de processo x64 para o modo solo. Todas as gravações validam bytes antes do patch.</summary>
internal sealed class ProcessMemory : IDisposable
{
    private const uint ProcessVmOperation = 0x0008;
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessVmWrite = 0x0020;
    private const uint ProcessQueryInformation = 0x0400;
    private const uint MemCommit = 0x1000, MemReserve = 0x2000, MemRelease = 0x8000;
    private const uint PageExecuteReadWrite = 0x40;
    private readonly IntPtr _handle;
    private readonly string _modulePath;
    private readonly ulong _moduleBase;
    private readonly uint _moduleSize;
    private readonly List<AppliedPatch> _patches = [];
    private readonly List<RemoteBlock> _allocations = [];
    private readonly Dictionary<string, ulong> _signatures = new(StringComparer.Ordinal);
    private bool _disposed;

    public int ProcessId { get; }
    public ulong ModuleBase => _moduleBase;
    public uint ModuleSize => _moduleSize;
    public string ModulePath => _modulePath;

    public ProcessMemory(int pid)
    {
        var process = Process.GetProcessById(pid);
        if (process.HasExited) throw new InvalidOperationException("O processo GRW.exe já foi encerrado.");
        ProcessId = pid;
        _modulePath = process.MainModule?.FileName ?? throw new InvalidOperationException("Não consegui obter o caminho do módulo GRW.exe.");
        _moduleBase = unchecked((ulong)process.MainModule!.BaseAddress.ToInt64());
        _moduleSize = unchecked((uint)process.MainModule.ModuleMemorySize);
        _handle = OpenProcess(ProcessVmOperation | ProcessVmRead | ProcessVmWrite | ProcessQueryInformation, false, pid);
        if (_handle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "Não consegui abrir GRW.exe com as permissões de memória necessárias.");
        using var stream = File.OpenRead(_modulePath);
        using var pe = new PEReader(stream);
        var header = pe.PEHeaders.PEHeader ?? throw new InvalidDataException("O arquivo selecionado não é um executável PE válido.");
        if (header.Magic != PEMagic.PE32Plus) throw new InvalidDataException("GRW.exe não é x64; esta versão só aceita processo x64.");
    }

    public byte[] Read(ulong address, int count)
    {
        ThrowIfDisposed();
        if (count < 0 || count > 16 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(count));
        var buffer = new byte[count];
        if (!ReadProcessMemory(_handle, unchecked((IntPtr)(long)address), buffer, (nuint)count, out var read) || read != (nuint)count)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Leitura incompleta em 0x{address:X}.");
        return buffer;
    }

    public bool TryRead(ulong address, byte[] buffer)
    {
        ThrowIfDisposed();
        return ReadProcessMemory(_handle, unchecked((IntPtr)(long)address), buffer, (nuint)buffer.Length, out var read) && read == (nuint)buffer.Length;
    }

    public byte ReadByte(ulong address) => Read(address, 1)[0];
    public int ReadInt32(ulong address) => BitConverter.ToInt32(Read(address, 4));
    public ulong ReadUInt64(ulong address) => BitConverter.ToUInt64(Read(address, 8));
    public float ReadFloat(ulong address) => BitConverter.ToSingle(Read(address, 4));

    public void Write(ulong address, byte[] bytes)
    {
        ThrowIfDisposed();
        if (!WriteProcessMemory(_handle, unchecked((IntPtr)(long)address), bytes, (nuint)bytes.Length, out var written) || written != (nuint)bytes.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Gravação incompleta em 0x{address:X}.");
        FlushInstructionCache(_handle, unchecked((IntPtr)(long)address), (nuint)bytes.Length);
    }

    public void WriteFloat(ulong address, float value) => Write(address, BitConverter.GetBytes(value));

    public IReadOnlyList<ulong> Scan(string pattern, bool executableOnly = true)
    {
        ThrowIfDisposed();
        if (executableOnly && _signatures.TryGetValue(pattern, out var cached)) return [cached];
        using var stream = File.OpenRead(_modulePath);
        using var pe = new PEReader(stream);
        var sections = pe.PEHeaders.SectionHeaders
            .Where(s => !executableOnly || (s.SectionCharacteristics & SectionCharacteristics.MemExecute) != 0)
            .Where(s => s.VirtualAddress >= 0 && s.VirtualSize > 0)
            .ToArray();
        var bytes = ParsePattern(pattern);
        var hits = new List<ulong>();
        foreach (var section in sections)
        {
            var size = Math.Min((uint)section.VirtualSize, _moduleSize - Math.Min(_moduleSize, (uint)section.VirtualAddress));
            if (size < bytes.Length) continue;
            const int chunkSize = 4 * 1024 * 1024;
            var overlap = Math.Max(0, bytes.Length - 1);
            for (uint offset = 0; offset < size;)
            {
                var take = (int)Math.Min((uint)chunkSize, size - offset);
                if (offset + take < size) take = Math.Min((int)size, take + overlap);
                var sectionAddress = _moduleBase + (uint)section.VirtualAddress + offset;
                var chunk = new byte[take];
                if (!TryRead(sectionAddress, chunk))
                {
                    offset += (uint)Math.Max(1, take - overlap);
                    continue;
                }
                for (var i = 0; i <= chunk.Length - bytes.Length; i++)
                {
                    var match = true;
                    for (var j = 0; j < bytes.Length; j++)
                        if (bytes[j] is byte expected && chunk[i + j] != expected) { match = false; break; }
                    if (match)
                    {
                        var address = sectionAddress + (uint)i;
                        if (hits.Count == 0 || hits[^1] != address) hits.Add(address);
                        if (hits.Count > 1) return hits;
                    }
                }
                offset += (uint)Math.Max(1, take - overlap);
            }
        }
        if (hits.Count == 1) _signatures[pattern] = hits[0];
        return hits;
    }

    public ulong FindUnique(string pattern)
    {
        var hits = Scan(pattern);
        if (hits.Count != 1) throw new InvalidOperationException($"Assinatura inválida para esta build: {hits.Count} resultado(s). Nenhuma memória foi alterada.");
        return hits[0];
    }

    public void Patch(string key, ulong address, byte[] expected, byte[] replacement)
    {
        if (expected.Length != replacement.Length) throw new ArgumentException("Patch e bytes esperados precisam ter o mesmo tamanho.");
        var current = Read(address, expected.Length);
        if (!current.AsSpan().SequenceEqual(expected)) throw new InvalidOperationException($"Patch {key} recusado: os bytes não correspondem ao esperado.");
        WriteProtected(address, replacement);
        var after = Read(address, replacement.Length);
        if (!after.AsSpan().SequenceEqual(replacement))
        {
            WriteProtected(address, current);
            throw new InvalidOperationException($"Patch {key} não passou na verificação; bytes restaurados.");
        }
        _patches.Add(new AppliedPatch(key, address, current, replacement));
    }

    public void WriteTracked(string key, ulong address, byte[] replacement)
    {
        var original = Read(address, replacement.Length);
        WriteProtected(address, replacement);
        _patches.Add(new AppliedPatch(key, address, original, replacement));
    }

    public void Restore(string key)
    {
        foreach (var patch in _patches.Where(p => p.Key == key).Reverse().ToArray())
        {
            var current = Read(patch.Address, patch.Replacement.Length);
            if (current.AsSpan().SequenceEqual(patch.Replacement)) WriteProtected(patch.Address, patch.Original);
            _patches.Remove(patch);
        }
    }

    public ulong AllocateNear(ulong address, int size)
    {
        var granularity = 0x10000UL;
        var aligned = address & ~(granularity - 1);
        for (ulong step = 0; step < 0x7FFF0000UL; step += granularity)
        {
            foreach (var candidate in step == 0 ? new[] { aligned } : new[] { aligned + step, aligned > step ? aligned - step : 0 })
            {
                if (candidate == 0 || Distance(candidate, address) > 0x7FFF0000UL) continue;
                var p = VirtualAllocEx(_handle, unchecked((IntPtr)(long)candidate), (nuint)size, MemCommit | MemReserve, PageExecuteReadWrite);
                if (p == IntPtr.Zero) continue;
                var result = unchecked((ulong)p.ToInt64());
                _allocations.Add(new RemoteBlock(result, size));
                return result;
            }
        }
        throw new InvalidOperationException("Não foi possível reservar uma área de código próxima ao ponto de hook.");
    }

    public void Hook(string key, ulong site, byte[] prefix, byte[] instrumentation)
    {
        var check = Read(site, prefix.Length);
        if (!check.AsSpan().SequenceEqual(prefix)) throw new InvalidOperationException($"Hook {key} recusado: bytes iniciais não conferem.");
        var originalBytes = Read(site, 64);
        var decoder = Iced.Intel.Decoder.Create(64, new ByteArrayCodeReader(originalBytes));
        decoder.IP = site;
        var instructions = new List<Instruction>();
        var stolen = 0;
        while (stolen < 5)
        {
            var instruction = decoder.Decode();
            if (instruction.Code == Code.INVALID || instruction.Length == 0) throw new InvalidOperationException($"Instrução inválida em hook {key}.");
            instructions.Add(instruction);
            stolen += instruction.Length;
            if (stolen > 32) throw new InvalidOperationException("Bloco de instruções muito extenso; hook recusado.");
        }
        var old = originalBytes[..stolen];
        var cave = AllocateNear(site, 0x1000);
        var relocated = EncodeRelocated(instructions, cave + (uint)instrumentation.Length);
        var backJumpAddress = cave + (uint)instrumentation.Length + (uint)relocated.Length;
        var back = EncodeRelativeJump(backJumpAddress, site + (uint)stolen);
        var code = instrumentation.Concat(relocated).Concat(back).ToArray();
        Write(cave, code);
        var patch = EncodeRelativeJump(site, cave);
        if (patch.Length > stolen) throw new InvalidOperationException("Hook sem espaço para salto.");
        Array.Resize(ref patch, stolen);
        for (var i = 5; i < patch.Length; i++) patch[i] = 0x90;
        Patch(key, site, old, patch);
    }

    private static byte[] EncodeRelocated(List<Instruction> instructions, ulong address)
    {
        var writer = new MemoryCodeWriter();
        var block = new InstructionBlock(writer, instructions, address);
        if (!BlockEncoder.TryEncode(64, block, out var message, out _)) throw new InvalidOperationException("Não foi possível realocar as instruções do hook: " + message);
        return writer.ToArray();
    }

    private static byte[] EncodeRelativeJump(ulong from, ulong to)
    {
        var displacement = unchecked((long)to - (long)(from + 5));
        if (displacement < int.MinValue || displacement > int.MaxValue) throw new InvalidOperationException("Salto relativo fora do alcance.");
        return [0xE9, .. BitConverter.GetBytes((int)displacement)];
    }

    private static ulong Distance(ulong a, ulong b) => a > b ? a - b : b - a;
    private static byte?[] ParsePattern(string pattern) => pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Select<string, byte?>(part => part is "?" or "??" ? null : Convert.ToByte(part, 16)).ToArray();

    private void WriteProtected(ulong address, byte[] bytes)
    {
        if (!VirtualProtectEx(_handle, unchecked((IntPtr)(long)address), (nuint)bytes.Length, PageExecuteReadWrite, out var old))
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Não foi possível liberar escrita em 0x{address:X}.");
        try { Write(address, bytes); }
        finally { VirtualProtectEx(_handle, unchecked((IntPtr)(long)address), (nuint)bytes.Length, old, out _); }
    }

    private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(ProcessMemory)); }
    public void Dispose()
    {
        if (_disposed) return;
        foreach (var patch in _patches.ToArray())
        {
            try { Restore(patch.Key); } catch { }
        }
        foreach (var block in _allocations)
            VirtualFreeEx(_handle, unchecked((IntPtr)(long)block.Address), 0, MemRelease);
        CloseHandle(_handle);
        _disposed = true;
    }

    private sealed record AppliedPatch(string Key, ulong Address, byte[] Original, byte[] Replacement);
    private sealed record RemoteBlock(ulong Address, int Size);

    private sealed class MemoryCodeWriter : CodeWriter
    {
        private readonly List<byte> _bytes = [];
        public override void WriteByte(byte value) => _bytes.Add(value);
        public byte[] ToArray() => [.. _bytes];
    }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, int processId);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadProcessMemory(IntPtr process, IntPtr address, [Out] byte[] buffer, nuint size, out nuint read);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] buffer, nuint size, out nuint written);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, nuint size, uint allocationType, uint protect);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool VirtualFreeEx(IntPtr process, IntPtr address, nuint size, uint freeType);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool VirtualProtectEx(IntPtr process, IntPtr address, nuint size, uint newProtect, out uint oldProtect);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, nuint size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
}
