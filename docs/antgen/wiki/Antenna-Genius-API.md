The Antenna Genius (AG) Ethernet API has a number of distinct pieces:

1. [Discovery protocol](Antenna-Genius-Discovery-protocol) which announces the AG's presence on the local network for facilitating client connections sent to UDP port 9007
2. The [Antenna Genius TCPIP API](Antenna-Genius-TCPIP-API) which is used to command the device and receive status information from the AG. The command API uses TCP/IP port 9007.
