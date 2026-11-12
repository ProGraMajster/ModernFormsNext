# Control parent notifications (#164)

Assigning `child.Parent = owner` uses the owner's canonical `Controls.Add` path.
That path already publishes `ParentChanged`; the property setter no longer publishes
an additional event after it returns. The previous extra call could also report a
stale outer operation after a handler redirected, removed or disposed the child.

The existing detach-then-attach contract is preserved:

```csharp
var first = new Panel();
var second = new Panel();
var child = first.Controls.Add(new Button());
child.ParentChanged += (_, _) => Console.WriteLine(child.Parent?.Name ?? "detached");

child.Parent = second; // One detached notification, then one attached notification.
child.Parent = second; // No notification.
child.Parent = null;   // One detached notification.
```

Use this API on the UI thread. Inside a handler, `Parent` and the owning collection
already agree. The property setter and collection API use the same assignment path,
including input retirement, inherited resources/commands and layout. A reentrant
assignment made while detaching wins over an older pending assignment. Throwing
observers do not roll back a committed assignment. Disposal retains the existing
ownership contract; it is not newly defined as detachment by this fix.

Constructor-owned implicit children still use `SetParentInternal`, and runtime-owned
implicit children still use the full collection assignment path. The existing
generation guard in the null setter remains intact. No public member, platform
boundary, renderer, template or startup behavior is replaced.

## Regression evidence

`ControlParentNotificationTests` covers attach/no-op, property and collection
detach/reparent parity, constructor-owned implicit children, redirect during detach,
redirect/remove/dispose during attach, and throwing observers including redirect
followed by failure. Each notification also checks canonical collection membership.

Before the fix, **8 of 13** cases failed on duplicate notifications; five cases passed.
The original failing TRX is retained separately from subsequent qualification.
The focused post-fix and full-suite results are recorded with the implementing PR.
Existing focus, resource, command, layout and text-input retirement tests remain in
the full suites; this correction adds no separate ownership or input machinery.

This is an event/ownership correction rather than a visual feature. ControlGallery
and template changes are not needed, and no manual visual verification is claimed.
