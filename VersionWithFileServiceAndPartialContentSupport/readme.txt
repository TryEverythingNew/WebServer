The file system is set by default to be 
"/home/xsun/Downloads" + Path.DirectorySeparatorChar + "test"
which means that, if you want to test some files in this system, please manually move or copy files or directories into this root directory.
You can also set this file system root by yourself in Program.cs and line 18.

My server supports unicode encoding, which means you can transfer any character, but to do this you need to go to WebServer.cs line 480 and set 
utf8_enabled to true.

Final hint:
To support partial content, please use FireFox browser and turn off your firewall.
