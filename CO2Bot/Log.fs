module CO2Bot.Log

open CO2Bot.Config

open Serilog
open Serilog.Core

let globalLogSwitch = LoggingLevelSwitch()
globalLogSwitch.MinimumLevel <- Config.getConfig().App.LogLevel

let Log =
    LoggerConfiguration().MinimumLevel.ControlledBy(globalLogSwitch).WriteTo.Console().CreateLogger()
