### GET
This command is used to get the configuration properties of the device
```
C<seq_number>|conf get
```
Example:
```
C64|conf get
```
Response Example:
```
C64|0|conf name=Antenna_Genius inband_port=0

<name>         = user-defined network name
<inband_port>  = inband port number (0=not configured, 1=A, 2=B)
```
### SET
This command is used to set the configuration properties of the device
```
C<seq_number>|conf set [name=<name>] [inband_port=<port_number>]

<name>         = user-defined network name
<port_number>  = inband port number (0=not configured, 1=A, 2=B)
```
Example:
```
C7|conf set name=Ranko_4O3A inband_port=2
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the device

Response Example:
```
R7|0|
```
### INIT
This command is used to initialize device configuration to a factory default.

Note: This command will [reboot](Antenna-Genius-TCPIP-reboot) the device automatically after the command is executed.
```
C<seq_number>|conf init
```
Example:
```
C64|conf init
```
Response Example:
```
C64|0|
```
Approximately 1 second after the command is executed, the device will reboot and initialize the configuration to a factory default.