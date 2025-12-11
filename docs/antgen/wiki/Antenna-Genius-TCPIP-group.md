### LIST
This command is used to list the available output groups.

```
C<seq_number>|group list
```
Example:
```
C56|group list
```
The command will return the list of configured output groups in multiple responses with the same <seq_number>, containing a message with group information prior to returning the OK status.

See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the device

Response Example:
```
R56|0|group 1 in_use=1 name=Group_1 mode=ANT antenna=1 allow_none=1
R56|0|group 2 in_use=1 name=Group_2 mode=BAND bands=0004 allow_none=1
R56|0|
```
| Parameter | Value|
|---|---|
|id|Group identifier|
|in_use|Is the group in use|
|name|User-defined name|
|mode|Group mode / ANT or BAND|
|antenna|Group active when antenna port selected|
|bands|Bands mask (hex) - the group is active when any of the masked band is active|
|allow_none|Allow no active output inside the group|

### SET
Configures output group
```
C<seq_number>|group set <group_id> in_use=<in_use> name=<name> mode=<ANT|BAND> [antenna=<antenna>|bands=<bands>] allow_none=<allow_none>

<group_id>    = Group identifier
<in_use>      = Group is in use
<name>        = User-defined group name
<mode>        = Group mode / ANT or BAND
<antenna>     = Antenna selection (ANT mode)
<bands>       = Band(s) selection (BAND mode)
<allow_none>  = Allow no active output selected in the group
```
Example:
```
C17|group set 2 in_use=1 name=Group_2 mode=BAND bands=3 allow_none=1
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the device

Response Example:
```
R17|0|
```