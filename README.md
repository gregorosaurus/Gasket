# Gasket

Gasket is used to output activity/pipeline run data including billing time, data transfer time, and pricing (in USD) for either Synapse 
workspaces or Azure Data Factories.  It is built with dotnet core, which allows it to be used on Windows, macOS, or Linux.

## Operation

Gasket is a simple command line interface program.  It will prompt you for input and for which workspace to output data for.  Currently, Gasket queries all pipeline activity runs and adds those to the output report.  
> In a future version, Gasket will allow for command line arguments so the report could be automated. 

<img src="img/2021-08-31 08_53_09-Gasket - Microsoft Visual Studio.png"/>

<img src="img/2021-08-31 08_54_14-Gasket - Microsoft Visual Studio.png"/>


## Building and Running

1. Clone the git repo or download the source files through github. 
2. Build: ```dotnet build src\Gasket.sln```
3. Run: ```dotnet src\Gasket\bin\Debug\net5.0\Gasket.dll```

## Sample Report

The report outputted is a CSV file that can easily be consumed by BI tools such as PowerBI. 

<img src="img/2021-08-31 09_03_41-gmsyn_activities_20210826181109.xlsx - Excel.png">

<img src="img/2021-08-31 09_01_18-GasketExamplePowerBIDashboard.pdf.png">