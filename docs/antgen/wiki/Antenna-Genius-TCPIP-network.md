### GET
Get device network configuration
```
C<seq_number>|network get
```
Example:
```
C34|network get
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the radio

Response Example:
```
R34|0|network dhcp=1 address=192.168.1.39 netmask=255.255.255.0 gateway=192.168.1.1 auth=213456
```
| Parameter | Value|
|---|---|
|dhcp|DHCP enabled (0 or 1)|
|address|Primary IP address|
|netmask|Network mask|
|gateway|Default gateway|
|auth|WAN auth password|
### SET
Configure device network parameters
```
C<seq_number>|network set [dhcp=0|1] [address=<address>] [netmask=<netmask>] [gateway=<gateway>] [auth=<auth>]

<dhcp>      = DHCP 1=enable or 0=disable
<address>   = Primary IP address
<netmask>   = Netmask
<gateway>   = Default gateway
<auth>      = Remote access password
```
Example:
```
C22|network dhcp=1 address=192.168.1.39 netmask=255.255.255.0 gateway=192.168.1.1 auth=123456
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the radio

Response Example:
```
R22|0|
```
When DHCP changes, the device will reboot automatically. Additionally, if DHCP is disabled and any of the addresses are changed, the device will reboot automatically.