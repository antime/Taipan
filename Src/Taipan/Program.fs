﻿namespace Taipan

open System
open System.Collections.Generic
open System.Threading.Tasks
open System.Threading
open System.IO
open System.Collections.Concurrent
open System.Linq
open System.Reflection
open System.Diagnostics
open Newtonsoft.Json
open Newtonsoft.Json.Serialization
open Argu
open Logging
open ES.Fslog
open ES.Fslog.Loggers
open ES.Taipan.Application
open ES.Taipan.Infrastructure.Network
open ES.Taipan.Infrastructure.Service

module Cli =    
    let mutable private _scanService : ScanService option = None

    type CLIArguments =
        | [<AltCommandLine("-u")>] Uri of String     
        | [<AltCommandLine("-p")>] Profile of String
        | [<AltCommandLine("-o")>] Output of String
        | Show_Profiles
        | Version 
        | Verbose
    with
        interface IArgParserTemplate with
            member s.Usage =
                match s with
                | Uri _ -> "the URI to scan."
                | Profile _ -> "profile name (or part of initial name) to use for the scan." 
                | Output _ -> "output report filename."
                | Show_Profiles -> "show all the currently available profiles."
                | Version -> "display Taipan version."
                | Verbose -> "print verbose messages."

    let private getOrDefault(key: String, defaultValue: String, d: Dictionary<String, String>) =
        if d.ContainsKey(key) 
        then d.[key]
        else defaultValue

    let printColor(msg: String, color: ConsoleColor) =
        Console.ForegroundColor <- color
        Console.WriteLine(msg)
        Console.ResetColor() 

    let printBanner() =
        Console.ForegroundColor <- ConsoleColor.Cyan        
        let banner = "-=[ Taipan Web Application Security Scanner ]=-"
        let year = if DateTime.Now.Year = 2017 then "2017" else String.Format("2017-{0}", DateTime.Now.Year)
        let copy = String.Format("Copyright (c) {0} Enkomio {1}", year, Environment.NewLine)
        Console.WriteLine(banner)
        Console.WriteLine(copy)
        Console.ResetColor()

    let printUsage(body: String) =
        Console.WriteLine(body)

    let printError(errorMsg: String) =
        printColor(errorMsg, ConsoleColor.Red)

    let version() = 
        FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion

    let prettyPrintProfiles() =    
        let separator = "+" + "-".PadLeft(31, '-') + "+" + "-".PadLeft(81, '-') + "+"
        Console.WriteLine()
        Console.WriteLine("Available profiles:")
        Console.WriteLine(separator)
        Console.WriteLine(String.Format("| {0,-30}| {1, -80}|", "Name", "Description"))
        Console.WriteLine(separator)
        
        let truncate(txt: String, num: Int32) =
            new String(txt.Take(min num (txt.Length)) |> Seq.toArray)

        for profileFile in Directory.EnumerateFiles(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Profiles"), "*.xml") do
            try
                let profile = new TemplateProfile()
                let profileContent = File.ReadAllText(profileFile)                        
                profile.AcquireSettingsFromXml(profileContent)            
                Console.WriteLine(String.Format("| {0,-30}| {1, -80}|", truncate(profile.Name, 29), truncate(profile.Description, 79)))
            with _ -> ()

        Console.WriteLine(separator)

    let prettyPrintMetrics(metrics: ServiceMetrics) =
        Console.WriteLine("{0}Service: {1}", Environment.NewLine, metrics.ServiceName)
        match metrics.GetAll() with
        | [] -> Console.WriteLine("N/A")
        | metrics ->
            metrics
            |> List.iter(fun metric ->
                Console.WriteLine("[{0}] {1} = {2}", metric.TimeStamp, metric.Name, metric.Value)
            )
            
    let replLoop() =
        Task.Factory.StartNew(fun () ->            
            while true do
                let key = Console.ReadKey(true).KeyChar
                if _scanService.IsSome then
                    let scanService =_scanService.Value                    
                    if key = 'k' || key = 'K' then
                        scanService.AbortCurrentScan()
                    elif key = 'd' || key = 'D' then                    
                        Console.WriteLine("{0}*********************************************", Environment.NewLine)
                        Console.WriteLine("**************** DEBUG INFO *****************")
                        Console.WriteLine("*********************************************")
                        scanService.GetCurrenScanMetrics()
                        |> Seq.iter prettyPrintMetrics
                        Console.WriteLine()
                    else
                        match scanService.GetCurrenScanStatus() with
                        | Some scanResult -> 
                            let scan = scanResult.Scan
                            if key = 'p' || key = 'P' then
                                scan.Pause()
                            elif key = 'r' || key = 'R' then
                                scan.Resume()
                            elif key = 's' || key = 'S' then
                                scan.Stop()
                        | None -> ()
                else
                    Thread.Sleep(500)
        , TaskCreationOptions.LongRunning) |> ignore
    
    let runScan(url: String, template: TemplateProfile, queryId: Guid, logProvider: ILogProvider) =
        let scanContext = 
            new ScanContext(
                StartRequest = new WebRequest(url),
                Template = template
            )
    
        if _scanService.IsNone then
            _scanService <- Some <| new ScanService(logProvider)

        programLogger.ScanCommands()        
        _scanService.Value.GetScanResult(scanContext, queryId, logProvider)  

    let getScanServices() =
        _scanService.Value
            
    // scan manager
    let runScanFromTemplateContent(templateString: String, url: String, queryId: Guid, logProvider: ILogProvider) =
        let template = new TemplateProfile()
        template.AcquireSettingsFromXml(templateString)
        runScan(url, template, queryId, logProvider) |> ignore

    let completeScan(queryId: Guid) =
        if _scanService.IsSome then
            _scanService.Value.FreeScan(queryId)
        _scanService <- None
            
    let runScanWithTemplate(url: String, template: TemplateProfile, logProvider: ILogProvider) = 
        let queryId = Guid.NewGuid()
        let scanResult = runScan(url, template, queryId, logProvider)        
        completeScan(queryId)
        scanResult

    let loadProfile(profileName: String) =
        let mutable profile = new TemplateProfile()
        let mutable profileFound = false

        if not(String.IsNullOrEmpty(profileName)) then
            for profileFile in Directory.EnumerateFiles(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Profiles"), "*.xml") do       
                if Path.GetFileName(profileFile).StartsWith(profileName, StringComparison.OrdinalIgnoreCase) && not profileFound then
                    let profileContent = File.ReadAllText(profileFile)                        
                    profile.AcquireSettingsFromXml(profileContent)
                    profileFound <- true

            if not profileFound then
                printError("Unable to find profile: " + profileName)
                Environment.Exit(1)
        
            profile
        else
            // load the default profile or, if not found, one of the available
            let fullScanId = Guid.Parse("FA327046-7FFF-4934-8B45-9323FE47D209")
            for profileFile in Directory.EnumerateFiles(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Profiles"), "*.xml") do            
                if not profileFound then
                    profile <- new TemplateProfile()
                    let profileContent = File.ReadAllText(profileFile)                        
                    profile.AcquireSettingsFromXml(profileContent)

                if profile.Id = fullScanId then
                    profileFound <- true

            profile

    let generateJsonReport(scanResult: ScanResult, reportFilename: String option) =                
        let scanReport = new ScanReport(scanResult)
        scanReport.Save(reportFilename)

    let printResultToConsole(scanResult: ScanResult) =
        Console.WriteLine()
        
        // print server fingerprint
        let webServerFingerprint = scanResult.GetWebServerFingerprint()

        printColor("-= Web Server =-" , ConsoleColor.DarkCyan)
        Console.WriteLine("\t{0}", webServerFingerprint.Server)
        Console.WriteLine()
        
        let frameworks = webServerFingerprint.Frameworks
        if frameworks.Any() then
            printColor("-= Web Frameworks =-", ConsoleColor.DarkCyan)
            frameworks
            |> Seq.iter(fun framework ->
                Console.WriteLine("\t{0}", framework)
            )
            Console.WriteLine()

        let languages = webServerFingerprint.Languages
        if languages.Any() then
            printColor("-= Web Programming Language =-", ConsoleColor.DarkCyan)
            languages
            |> Seq.iter(fun lang ->
                Console.WriteLine("\t{0}", lang)
            )
            Console.WriteLine()
        
        // print hidden resources
        let hiddenResources = scanResult.GetHiddenResourceDiscovered()
        if hiddenResources.Any() then
            printColor("-= Hidden Resources =-", ConsoleColor.DarkCyan)
            hiddenResources
            |> Seq.iter(fun hiddenRes ->
                Console.WriteLine("\t{0} {1} ({2})", hiddenRes.BaseUri.AbsolutePath.PadRight(40), hiddenRes.Response.StatusCode, int32 hiddenRes.Response.StatusCode)
            )
            Console.WriteLine()

        // print fingerprint        
        let webApplicationIdentified = scanResult.GetWebApplicationsIdentified()
        if webApplicationIdentified.Any() then
            printColor("-= Identified Web Applications =-", ConsoleColor.DarkCyan)
            webApplicationIdentified
            |> Seq.iter(fun webApp ->
                let versions = String.Join(",", webApp.IdentifiedVersions |> Seq.map(fun kv -> kv.Key.Version))
                Console.WriteLine("\t{0} v{1} {2}", webApp.WebApplicationFingerprint.Name, versions, webApp.Request.Request.Uri.AbsolutePath)
            )
            Console.WriteLine()
        
        // print security issues
        let issues = scanResult.GetSecurityIssues()
        if issues.Any() then
            printColor("-= Security Issues =-", ConsoleColor.DarkCyan)
            issues
            |> Seq.iter(fun issue ->
                let paramName = getOrDefault("parameter", "N/A", issue.Details.Properties)
                Console.WriteLine("\t{0} Uri:{1} Parameter:{2}", issue.Name, issue.Uri.AbsoluteUri, paramName)
            )
            Console.WriteLine()

    let runScanWithTemplateName(urlToScan: String, profileName: String, isVerbose: Boolean) =
        let profile = loadProfile(profileName)
        match Uri.IsWellFormedUriString(urlToScan, UriKind.Absolute) with
        | false -> 
            printError(String.Format("Url {0} is not valid", urlToScan)) |> ignore
            None
        | true ->             
            let (logProvider, logFile) = configureLoggers(urlToScan, profile.Name, isVerbose)
            Some(runScanWithTemplate(urlToScan, profile, logProvider), logFile)

    [<EntryPoint>]
    let main argv = 
        printBanner()

        let parser = ArgumentParser.Create<CLIArguments>(programName = "taipan.exe")
        try            
            let results = parser.Parse(argv)
                    
            if results.IsUsageRequested then
                printUsage(parser.PrintUsage())
                0
            else
                let isVerbose = results.Contains(<@ Verbose @>)
                let showVersion = results.Contains(<@ Version @>)
                let showProfiles = results.Contains(<@ Show_Profiles @>)
                let urlToScan = results.TryGetResult(<@ Uri @>)
                let profileName = results.TryGetResult(<@ Profile @>)
                let outputReport = results.TryGetResult(<@ Output @>)
                
                if showProfiles then
                    prettyPrintProfiles()
                elif showVersion then
                    Console.WriteLine("Version: {0}", version())  
                else
                    match (urlToScan, profileName) with
                    | (Some urlToScan, Some profileName) ->
                        replLoop()
                        match runScanWithTemplateName(urlToScan, profileName, isVerbose) with
                        | Some (scanResult, logFile) ->
                            printResultToConsole(scanResult)                            
                            let reportFile = generateJsonReport(scanResult, outputReport)
                            programLogger.ReportSaved(reportFile)
                            programLogger.ScanCompleted(logFile)
                        | None -> ()

                    | _ -> printUsage(parser.PrintUsage())   
                0
        with 
            | :? ArguParseException ->
                printUsage(parser.PrintUsage())   
                1
            | e ->
                printError(e.ToString())
                1