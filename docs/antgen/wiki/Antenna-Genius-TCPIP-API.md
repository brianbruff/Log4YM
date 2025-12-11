The Antenna Genius (AG) TCP/IP API performs the following functions:

1. Issuing [commands](Antenna-Genius-TCPIP-API#antenna-genius-tcpip-commands) to the AG
2. Asynchronously receiving [status information](Antenna-Genius-TCPIP-API#antenna-genius-tcpip-statuses) from the AG
3. Locating AG devices via the [discovery protocol](Antenna-Genius-Discovery-protocol)

The command API is accessed using a TCP/IP socket connection to port 9007 of the AG. To locate AG devices on the local network, use the [discovery protocol](Antenna-Genius-Discovery-protocol)

## TCP/IP Command Protocol
### Command Prologue
Upon connecting to the client port, Antenna Genius will provide the firmware version installed, followed by the 'AG' identifier, and optional 'AUTH' phrase if the client connection is initiated from WAN. 

This information is provided each time the client connects to the Antenna Genius.
```
V<a.b.c> AG[ AUTH]

V          = indicates version information
<a.b.c>    = major version number in decimal '.' minor version number in decimal '.' build version number in decimal
AG         = indicates the type of the device (Antenna Genius = AG)
AUTH       = if the client connection is initiated from WAN, the client must authenticate itself
```
### Command Format
Once the connection is established, the client may begin sending commands to the device. Commands follow a general ASCII format. Command format (from client to device):
```
C<seq_number>|command<terminator>

C             = indicates a command
<seq_number>  = sequence number, numeric, 1 to 255
<terminator>  = 0x0D
```
### Response Format
All commands receive responses from the device. At least one response will be sent for each command received by the client. The response is tied to the command by the sequence number supplied by the client. This sequence number will be echoed back to the client in the response. The client should check the hex response to determine if the command issued was successful. A zero (0) value indicates success. Any other value represents a failure of the command to execute. The response value is unique and provides insight into the failure mode. It is recommended that any decisions that are made by the client based on a response should use the hexadecimal response to make the decisions. Response Format (from device to client):
```
R<seq_number>|<hex_response>|<message>

<seq_number>   = numeric, up to 32-bits -- echoed from the command sent from the client
<hex response> = ASCII hexadecimal number (32-bit) return value (see table below for possibilities)
<message>      = response value for parsing
```
A list of responses can be found on the [Known API responses](Known-API-responses) page.
### Status Format
Objects in the device will send out status when they are changed. To find out about objects, the client must have either subscribed to the status for the object. Status messages are asynchronous and can be sent to the client at any time. Status Format (from device to client):
```
S<0>|<message>

<0>         = reserved for future use
<message>   = status value for parsing
```
The period character is used as a decimal separator independent of the locale.

A detailed list of status responses can be found in [Status Responses](Antenna-Genius-TCPIP-API#tcpip-statuses)

## TCP/IP Commands
- [antenna](Antenna-Genius-TCPIP-antenna) → list and configure antenna port(s)
- [auth](Antenna-Genius-TCPIP-auth) → authenticate clients connected from outside of the local area network
- [band](Antenna-Genius-TCPIP-band) → list and configure band(s)
- [conf](Antenna-Genius-TCPIP-conf) → get configuration properties, factory reset the configuration
- [flex](Antenna-Genius-TCPIP-flex) → configuring integration with FlexRadio
- [group](Antenna-Genius-TCPIP-group) → list and configure output group(s)
- [info](Antenna-Genius-TCPIP-info) → get device basic info
- [keepalive](Antenna-Genius-TCPIP-keepalive) → enable or disable keepalive mechanism
- [network](Antenna-Genius-TCPIP-network) → network parameter configuration
- [output](Antenna-Genius-TCPIP-output) → list and configure output(s)
- [ping](Antenna-Genius-TCPIP-ping) → inform the keepalive mechanism that the client is active
- [port](Antenna-Genius-TCPIP-port) → list and configure radio port(s)
- [reboot](Antenna-Genius-TCPIP-reboot) → reboot the device
- [stack](Antenna-Genius-TCPIP-stack) → configuring AG stack
- [sub](Antenna-Genius-TCPIP-sub) → subscribe to a specific status messages
- [unsub](Antenna-Genius-TCPIP-unsub) → unsubscribe from a specific status messages
## TCP/IP Statuses
- [antenna](Antenna-Genius-Status-Messages#ANTENNA) → propagation of antenna changes
- [group](Antenna-Genius-Status-Messages#GROUP) → propagation of output group changes
- [output](Antenna-Genius-Status-Messages#OUTPUT) → propagation of output changes
- [port](Antenna-Genius-Status-Messages#PORT) → propagation of port changes
- [relay](Antenna-Genius-Status-Messages#RELAY) → propagation of output relay status