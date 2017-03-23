# Use at your own Risk
**Has not been tested thoroughly, may crash itself and injected processes**

### Why?

Certain applications on windows love to steal the focus. This is especially infuriating when you are typing something in Window A and Window B pops up and eats all your input.

Unfortunately Windows XP is the last windows version where this behaviour could be turned off.
AFAIK there is no solution for later windows versions anywhere.

I hacked this tool together to make working on windows less frustrating.

### What is it?

This software injects a dll into newly started Processes which hooks calls to [SetForegroundWindow][msdn]. The hooked call does not pass on to the Win api so that the Process does not steal Focus from another Process.
Instead it Flashes the Taskbar icon.

A Blacklist is used so that the injection happens only for processes which are configured.

After a process which is on the blacklist starts there is a 1 second delay before the dll gets injected to allow the process to start up. This is a workaround for crashes most processes experience on instant injection.

[msdn]: https://msdn.microsoft.com/en-us/library/windows/desktop/ms633539(v=vs.85).aspx

### Why does it need elevated permissions?

Subscription to the Win management api, which is used for subscribing to process creation events, needs elevated permissions

### How to

1) Edit the Config setting "Blacklist" to your liking
2) Start it

### Attributions
* [EasyHook][easyhook] by Christoph Husse / Justin Stenning
* [System.Management][systemmanagement] by bmars
* [Windows Turn Off Icon][icon] by Hopstarter

[easyhook]: https://easyhook.github.io/
[systemmanagement]: https://www.nuget.org/packages/System.Management/
[icon]: http://www.iconarchive.com/show/sleek-xp-software-icons-by-hopstarter/Windows-Turn-Off-icon.html
