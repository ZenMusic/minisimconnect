FS2020 SDK "C:\MSFS SDK"
64 bit assembly
post build event which copies the DLL and config:
```
xcopy "C:\MSFS SDK\SimConnect SDK\lib\SimConnect.dll" "$(TargetDir)" /y
xcopy "C:\MSFS SDK\Samples\SimvarWatcher\SimConnect.cfg" "$(TargetDir)" /y
```
required
Microsoft.FlightSimulator.SimConnect.dll