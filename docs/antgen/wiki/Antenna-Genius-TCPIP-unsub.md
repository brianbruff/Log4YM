### UNSUB
Unsubscribe stops a subscription that is in process for the objects that are listed in the unsub command. Subscriptions provide status messages for objects that are changed in the device, typically by the device itself. The general format of the unsubscribe command is:
```
C<seq_number>|unsub <object> <object_id|all>

<object>         = the object that is the target of the command such as 'port', 'output', or 'relay'
<object_id|all>  = the specific number or identifier of the object to be unsubscribed from or the word "all" to unsubscribe to all
```
The complete list of objects that can be subscribed to / unsubscribed from can be found in the [sub command page](Antenna-Genius-TCPIP-sub) noting that the command unsub should replace the sub command seen on that page.

See [Response Format](Antenna-Genius-TCPIP-API#Response-Format) for details on the format of the response messages from the radio
| Hex Response | Meaning |
|---|---|
| 0x00 | OK unsubscribed |
| 0x30 | Invalid subscription object |

Response Example:
```
R43|0|
```