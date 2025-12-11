Antenna Genius Discovery protocol broadcasts the device information on the local area network (LAN) via UDP on port 9007.

UDP broadcasted message is formatted as a string that starts with 'AG' followed by multiple property=value pairs separated by white space: _AG ip=%u.%u.%u.%u port=%u v=%u.%u.%u serial=%s name=%s ports=%u antennas=%u mode=%s uptime=%u_

Example:
```
AG ip=192.168.1.39 port=9007 v=4.0.22 serial=9A-3A-DC name=Ranko_4O3A ports=2 antennas=8 mode=master uptime=3034

ip        = LAN IP address for client connection(s)
port      = TCP port for client connection(s)
v         = firmware version installed
serial    = unique serial number generated based on network adapter MAC address (last six hex numbers)
name      = user-defined network name
ports     = number of radio ports available
antennas  = number of antenna ports available
mode      = AG stack operating mode [master|slave]
uptime    = number of seconds since the last power-on
```

UDP packet with the above information is broadcasted on the local area network every 1 second by every connected Antenna Genius device.