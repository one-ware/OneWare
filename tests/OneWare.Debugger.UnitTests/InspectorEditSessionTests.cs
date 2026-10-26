using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using OneWare.Debugger.Models;
using OneWare.Debugger.ViewModels;
using OneWare.Essentials.Debugger.Entities;
using OneWare.Essentials.Debugger.Interfaces;
using OneWare.Essentials.Services;
using Xunit;

namespace OneWare.Debugger.UnitTests;

public class InspectorEditSessionTests
{
    private readonly FakeDebuggerService _service = new();
    private readonly ValueFormatViewModel _valueFormat = new(DispatchProxy.Create<ISettingsService, NullSettings>());
    private readonly InspectorEditSession _session;

    public InspectorEditSessionTests()
    {
        _session = new InspectorEditSession(_service, _valueFormat);
    }

    private RegisterRow Register(string name, string value = "0x0012")
    {
        return new RegisterRow { Name = name, Value = value, Edit = new ValueEdit(_session) { Bits = 16 } };
    }

    [Fact]
    public void OnlyOneCellCanBeEditedAtATime()
    {
        var akku = Register("Akku");
        var pc = Register("Programmzaehler");

        _session.BeginEditCommand.Execute(akku);

        Assert.False(_session.BeginEditCommand.CanExecute(pc));

        _session.BeginEditCommand.Execute(pc);

        Assert.Same(akku, _session.Current);
        Assert.False(pc.Edit.IsEditing);
    }

    [Fact]
    public void EditingLocksTheNumberBaseUntilItEnds()
    {
        var akku = Register("Akku");

        _session.BeginEditCommand.Execute(akku);
        Assert.True(_valueFormat.IsLocked);

        _session.CancelCommand.Execute(null);
        Assert.False(_valueFormat.IsLocked);
    }

    [Fact]
    public void EditStartsWithTheDisplayedValue()
    {
        var akku = Register("Akku", "0x00ff");

        _session.BeginEditCommand.Execute(akku);

        Assert.True(akku.Edit.IsEditing);
        Assert.Equal("0x00ff", akku.Edit.Text);
    }

    [Fact]
    public void CancelLeavesTheValueUntouched()
    {
        var akku = Register("Akku");

        _session.BeginEditCommand.Execute(akku);
        akku.Edit.Text = "0x99";
        _session.CancelCommand.Execute(null);

        Assert.False(akku.Edit.IsEditing);
        Assert.False(_session.IsEditing);
        Assert.Equal("0x0012", akku.Value);
        Assert.Empty(_service.RegisterWrites);
    }

    [Fact]
    public async Task SaveWritesTheParsedValueAndEndsTheEdit()
    {
        var akku = Register("Akku");

        _session.BeginEditCommand.Execute(akku);
        akku.Edit.Text = "0x34";
        await _session.SaveCommand.ExecuteAsync(null);

        Assert.Equal([("Akku", 0x34UL)], _service.RegisterWrites);
        Assert.False(akku.Edit.IsEditing);
        Assert.Null(_session.Current);
    }

    [Fact]
    public async Task RefusedWriteStaysInEditModeWithTheReason()
    {
        var flag = Register("Readonly_ALU_Flag_Equal_Zero");
        _service.NextResult = DebugWriteResult.Failed("The target refused to change this register.");

        _session.BeginEditCommand.Execute(flag);
        flag.Edit.Text = "1";
        await _session.SaveCommand.ExecuteAsync(null);

        Assert.True(flag.Edit.IsEditing);
        Assert.Same(flag, _session.Current);
        Assert.Equal("The target refused to change this register.", flag.Edit.Error);
    }

    [Fact]
    public async Task InvalidInputStaysInEditModeWithoutWriting()
    {
        var akku = Register("Akku");

        _session.BeginEditCommand.Execute(akku);
        akku.Edit.Text = "xyz";
        await _session.SaveCommand.ExecuteAsync(null);

        Assert.True(akku.Edit.IsEditing);
        Assert.Equal("\"xyz\" is not a valid hexadecimal number.", akku.Edit.Error);
        Assert.Empty(_service.RegisterWrites);
    }

    [Fact]
    public void LockedRowCannotBeEdited()
    {
        var flag = Register("Readonly_ALU_Flag_Equal_Zero");
        flag.Edit.LockReason = "Read-only register";

        Assert.False(_session.BeginEditCommand.CanExecute(flag));
    }

    [Fact]
    public void NothingCanBeEditedWhileTheTargetRuns()
    {
        _service.State = new DebugSessionState { IsRunning = true };

        Assert.False(_session.BeginEditCommand.CanExecute(Register("Akku")));
    }

    [Fact]
    public void TargetStartingToRunCancelsTheEdit()
    {
        var akku = Register("Akku");
        _session.BeginEditCommand.Execute(akku);

        _service.State = new DebugSessionState { IsRunning = true };
        _service.RaiseStateChanged();

        Assert.False(akku.Edit.IsEditing);
        Assert.False(_session.IsEditing);
    }

    [Fact]
    public async Task MemoryIsWrittenInTheTargetsByteOrder()
    {
        _service.TargetProfile = new DebugTargetProfile { AddressableUnitBytes = 2, IsLittleEndian = true };
        var word = new MemoryRow { Address = "0x10", Value = "0000", Edit = new ValueEdit(_session) { Bits = 16 } };

        _session.BeginEditCommand.Execute(word);
        word.Edit.Text = "1234";
        await _session.SaveCommand.ExecuteAsync(null);

        var (address, bytes) = Assert.Single(_service.MemoryWrites);
        Assert.Equal("((long)(0x10))*2", address);
        Assert.Equal([0x34, 0x12], bytes);
    }

    [Fact]
    public async Task VariableIsWrittenSignExtended()
    {
        var counter = new VariableRow { Name = "counter", Value = "0000", Edit = new ValueEdit(_session) { Bits = 16 } };

        _session.BeginEditCommand.Execute(counter);
        counter.Edit.Text = "0xFFFF";
        await _session.SaveCommand.ExecuteAsync(null);

        Assert.Equal([("counter", -1L)], _service.VariableWrites);
    }

    public class NullSettings : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var type = targetMethod?.ReturnType;
            return type is { IsValueType: true } && type != typeof(void) ? Activator.CreateInstance(type) : null;
        }
    }

    private class FakeDebuggerService : IDebuggerService
    {
        public List<(string Name, ulong Value)> RegisterWrites { get; } = [];
        public List<(string Address, byte[] Bytes)> MemoryWrites { get; } = [];
        public List<(string Name, long Value)> VariableWrites { get; } = [];
        public DebugWriteResult NextResult { get; set; } = DebugWriteResult.Ok;

        public IReadOnlyList<IDebugSessionLauncher> SessionLaunchers => [];
        public IReadOnlyList<IDebugTargetPreparer> TargetPreparers => [];
        public IDebugSession? CurrentSession => null;
        public DebugSessionState State { get; set; } = DebugSessionState.Empty;
        public bool IsActive { get; set; } = true;
        public DebugTargetProfile TargetProfile { get; set; } = DebugTargetProfile.Default;

        public event EventHandler? StateChanged;

        public void RaiseStateChanged()
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public Task<DebugWriteResult> WriteRegisterAsync(string name, ulong value)
        {
            RegisterWrites.Add((name, value));
            return Task.FromResult(NextResult);
        }

        public Task<DebugWriteResult> WriteMemoryAsync(string address, byte[] bytes)
        {
            MemoryWrites.Add((address, bytes));
            return Task.FromResult(NextResult);
        }

        public Task<DebugWriteResult> WriteVariableAsync(string name, long value)
        {
            VariableWrites.Add((name, value));
            return Task.FromResult(NextResult);
        }

        public void RegisterSessionLauncher<T>() where T : IDebugSessionLauncher => throw new NotSupportedException();
        public void RegisterTargetPreparer<T>() where T : IDebugTargetPreparer => throw new NotSupportedException();
        public Task<bool> StartAsync(DebugLaunchRequest launchRequest) => throw new NotSupportedException();
        public Task<bool> StartAsync(IDebugTargetPreparer preparer) => throw new NotSupportedException();
        public Task StopAsync() => throw new NotSupportedException();
        public Task ContinueAsync() => throw new NotSupportedException();
        public Task PauseAsync() => throw new NotSupportedException();
        public Task StepIntoAsync() => throw new NotSupportedException();
        public Task StepOverAsync() => throw new NotSupportedException();
        public Task StepOutAsync() => throw new NotSupportedException();
        public Task<string?> ReadMemoryAsync(string address, int byteCount) => throw new NotSupportedException();
        public Task SendRawCommandAsync(string command) => throw new NotSupportedException();
    }
}
