@echo off

rem Written by Matthew Monroe for PNNL (Richland, WA)
rem Started March 3, 2006
rem Last Updated March 31, 2006

:ChangePath
cd C:\DMS_Programs\MSFileInfoScanner\

:ParseDateAndTimeAtStart
rem date /t returns Day_of_Week, Month, Day and year in my install. Alter the set statements if your install uses a different date format.
for /f "tokens=1,2,3,4* delims=/ " %%i in ('date /t') do set TDDAY=%%i&set TDMM=%%j&set TDDD=%%k&set TDYY=%%l
for /f "tokens=1,2,3* delims=: " %%i in ('time /t') do set TDHH=%%i&set TDMIN=%%j&set TDAMPM=%%k

:LogStart
echo %TDYY%-%TDMM%-%TDDD% %TDHH%:%TDMIN% %TDAMPM%, Starting >> Logs\MSFileInfoScannerLog_%TDYY%-%TDMM%.txt

:ReadAndUpdateCounter
rem Read the counter value from RunCounter.txt
for /f %%i in (RunCounter.txt) do set RUNCOUNTER=%%i

rem Increment the counter
set /a NEWVALUE=RUNCOUNTER+1

rem Write the new value back into RunCounter.txt
echo %NEWVALUE% > RunCounter.txt

rem Compute the modulus of the value
rem I should be able to use the modulus operator (%) to do this, but it's not working
rem The following produces the same result
set /a MODULUS=NEWVALUE-(NEWVALUE/20)*20

rem When the Modulus is 0, then check all folders
rem Otherwise, only check the folders for the new datasets
if "%MODULUS%"=="0" (Goto CheckAllFolders) Else (Goto CheckNewDatasets)

:CheckNewDatasets
osql -S gigasax -d DMS5 -E -w 255 -n -i GetFolderList_Last2Days.sql -o GetFoldersLast2Days.bat
Call GetFoldersLast2Days.bat
Goto CopyFile

:CheckAllFolders
osql -S gigasax -d DMS5 -E -w 255 -n -i GetFolderList_Last6Months.sql -o GetFoldersLast6Months.bat
Call GetFoldersLast6Months.bat
Goto CopyFile

:CopyFile
Copy DatasetTimeFile.txt \\pogo\AcqTimeStats\DatasetTimeFile.txt /Y

:ParseDateAndTimeAtEnd
rem date /t returns Day_of_Week, Month, Day and year in my install. Alter the set statements if your install uses a different date format.
for /f "tokens=1,2,3,4* delims=/ " %%i in ('date /t') do set TDDAY=%%i&set TDMM=%%j&set TDDD=%%k&set TDYY=%%l
for /f "tokens=1,2,3* delims=: " %%i in ('time /t') do set TDHH=%%i&set TDMIN=%%j&set TDAMPM=%%k

:LogEnd
echo %TDYY%-%TDMM%-%TDDD% %TDHH%:%TDMIN% %TDAMPM%, Complete >> Logs\MSFileInfoScannerLog_%TDYY%-%TDMM%.txt
