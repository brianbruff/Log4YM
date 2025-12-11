### LIST
This command is used to list the available outputs.

```
C<seq_number>|output list
```
Example:
```
C1|output list
```
The command will return the list of configured outputs in multiple responses with the same <seq_number>, containing a message with output configuration prior to returning the OK status.

See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the device

Response Example:
```
R1|0|output 1 in_use=1 group=1 name=Output_1 state=01 hotkey=0 trx=0
R1|0|output 2 in_use=1 group=1 name=Output_2 state=08 hotkey=0 trx=0
R1|0|output 3 in_use=1 group=1 name=Output_3 state=40 hotkey=0 trx=0
R1|0|
```
| Parameter | Value|
|---|---|
|id|Group identifier|
|in_use|Is the output in use|
|group|Output parent identifier|
|name|User-defined name|
|state|Relay state when output is active (hex)|
|hotkey|Global keyboard combination (for Windows Utility)|
|trx|Output toggles (turns off) other output(s) in the same group|

### SET
Configures outputs
```
C<seq_number>|output set <output_id> [(active=<0|1>)|(in_use=<in_use> group=<group_id> name=<name> state=<state> hotkey=<hotkey> trx=<trx>)]

<output_id>   = Output identifier
<active>      = Activated/deactivates output
<in_use>      = Group is in use
<group_id>    = Parent group identifier
<name>        = User-defined group name
<state>       = Relay state when output is active (hex)
<hotkey>      = Global keyboard combination (for Windows Utility)
<trx>         = Output toggles (turns off) other output(s) in the same group
```
Example:
```
C17|output set 1 in_use=1 group=1 name=Output_1 mask=01 state=01 hotkey=0 trx=0

C18|output set 1 active=1
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the device

Response Example:
```
R17|0|

R18|0|
```
### INIT
Initializes output configuration - mandatory before configuring group(s) and output(s).
```
C<seq_number>|output init
```
Example:
```
C67|output init
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the device

Response Example:
```
R67|0|
```