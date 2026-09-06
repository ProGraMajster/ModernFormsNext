# Issue #56 Phase 1 audit

Baseline: `b290a13da297401c93879b32f755b03a84a2c776` (master and origin/master).
Scope: command foundation only. Issue #56 remains open.

| Area | Existing behavior | Missing for Phase 1 | Proposed change |
| --- | --- | --- | --- |
| ICommand | `DataBinding/ICommandBindingTargetProvider.cs` contains an internal default-interface implementation, but no type implements it at this baseline. The issue's statement that selected controls already expose ICommand is not reflected in this checkout. | A working public consumer and safe lifecycle. | Evolve this binding path into a shared source helper; retain System.Windows.Input.ICommand. |
| Existing Command | Internal `DataBinding.Command` is a weak native-style ID registry for the public `ICommandExecutor` contract. It is not an application ICommand implementation. | Reusable delegate-based application actions. | Add only `DelegateCommand`; leave the ID registry and ICommandExecutor intact. |
| Subscription / execution | The unused helper subscribes directly, restores a saved Enabled value on removal, and calls Execute without CanExecute. No disposal, parameter requery, or dispatcher handling is wired to a control. | Deterministic replacement, removal, disposal, parameter evaluation and action-time guards. | Centralize these responsibilities in the existing DataBinding area. Use weak event subscriptions plus explicit detach. |
| Enabled | Control stores local intent in States.Enabled and combines it with Parent.Enabled. The unused helper overwrites local intent. The setter compares old effective state with the requested local value. | Independent command availability, correct effective notifications. | Preserve the local bit; add a command-disabled bit and compare effective values after changes. |
| Button | No Command/CommandParameter. OnClick applies DialogResult then raises Click. PerformClick calls OnClick; Space/Enter and semantic accessibility Invoke use PerformClick. | Reference command source. | Add properties, preserve DialogResult/Click order, run current command after Click with availability checks. |
| Menu / ToolBar / Ribbon / ContextMenu | Share MenuItem, which has Click and owner-dependent Enabled, but no IDisposable lifecycle. | Command lifetime cannot safely be added as a tiny property-only change. | Defer to Phase 4 rather than redesign ownership in Phase 1. |
| Tray menu | NotifyIconMenuItem has explicit Component disposal and guarded PerformClick. Backend uses that path. | Small representative non-Control action source is feasible. | Reuse the same helper, keeping native menu snapshot behavior unchanged. |
| Threading / errors | Application.RunOnUIThread posts to WindowKit's dispatcher; direct action exceptions propagate. | Marshal background requery without changing the exception policy. | Post notifications to the existing dispatcher; preserve original exceptions and recoverable fail-closed CanExecute state. |
| Accessibility | Shared Button peer exposes Invoke and unavailable state; Windows UIA and Android adapters consume shared semantics and PerformAction. | Command-backed regression evidence. | Test this normal action path without changing #59 architecture. |
| TestHost | Existing ModernFormsTestHost can show controls and explicitly drain its dispatcher. | Consumer command/threading regression. | Add ordinary tests, no input simulation or new host facilities. |
| Designer / editor actions | DesignerCommandService, VS command targets and MarkdownEditor commands are separate existing features. | Nothing in this phase. | Leave them unchanged. No Ctrl+S fix, serialization, routing, or shortcuts. |

## API decisions

One sealed public `DelegateCommand` implements System.Windows.Input.ICommand. Parameterless
Action/Func<bool> and parameter-aware Action<object?>/Predicate<object?> overloads cover Phase 1.
There is no second ICommand, public Command alias, generic conversion policy, command target,
global manager, routed binding, or async helper. Generic commands are deferred until there is a
concrete consumer that justifies additional nullability and conversion contracts.

Button is the reference source; NotifyIconMenuItem is the representative non-Control consumer.
The shared internal helper evolves the unused ICommandBindingTargetProvider path, so there is one
implementation of parameter requery, subscription lifetime and execution checks.

See [commands.md](commands.md) for the final public contract and examples.
