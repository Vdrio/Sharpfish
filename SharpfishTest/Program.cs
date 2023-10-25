// See https://aka.ms/new-console-template for more information
using Sharpfish;


Console.WriteLine("Hello, World!");
Stockfish fish = new Stockfish();
//fish.StockfishOutput += StockfishOutput;
fish.GoInfoReceived += Fish_StockfishGoInfoReceived;
fish.BestMoveReceived += Fish_BestMoveReceived;
fish.Initialize();
fish.Go(20);

void Fish_BestMoveReceived(string bestMove)
{
    Console.WriteLine("BEST MOVE: " + bestMove);
}

void Fish_StockfishGoInfoReceived(List<GoInfo> goInfos)
{
    foreach(GoInfo goInfo in goInfos)
    {
        Console.WriteLine(goInfo);
    }
}


Console.Read();
void StockfishOutput(string output)
{
    Console.WriteLine("From output: " + output);
}