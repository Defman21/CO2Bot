namespace CO2Bot.Services.Internal

open System.Threading

type IReceiverService =
    abstract member ReceiveAsync: CancellationToken -> Async<unit>
