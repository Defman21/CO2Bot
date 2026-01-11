namespace CO2Bot.Services.Internal

open System.Threading
open System.Threading.Tasks

type IReceiverService =
    abstract member Receive: CancellationToken -> Task<unit>
