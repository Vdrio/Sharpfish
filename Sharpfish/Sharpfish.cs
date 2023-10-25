using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Sharpfish;

public class Stockfish 
{

    private string dllPath = "";

    // Import the DLL functions.
    [DllImport("StockfishLib")]
    private static extern void RegisterCallback(StockfishCallbackDelegate callback);

    [DllImport("StockfishLib")]
    private static extern void IsReadyCpp();
    [DllImport("StockfishLib")]
    private static extern void InitializeEngineCpp();

    [DllImport("StockfishLib")]
    private static extern void GoCpp(StringBuilder parameters);
    [DllImport("StockfishLib")]
    private static extern void SetPositionCpp(StringBuilder parameters);
    [DllImport("StockfishLib")]
    private static extern void SetOptionCpp(StringBuilder name, StringBuilder value);

    [DllImport("StockfishLib")]
    private static extern void SetIsLibraryCpp(bool value);
    [DllImport("StockfishLib")]
    private static extern void UCICpp();
    [DllImport("StockfishLib")]
    private static extern void UcinewgameCpp();
    [DllImport("StockfishLib")]
    private static extern void QuitCpp();
    [DllImport("StockfishLib")]
    private static extern void StopCpp();
    [DllImport("StockfishLib")]
    private static extern void PonderHitCpp();
    [DllImport("StockfishLib")]
    private static extern void BenchCpp(StringBuilder parameters);
    [DllImport("StockfishLib")]
    private static extern void DCpp();
    [DllImport("StockfishLib")]
    private static extern void FlipCpp();
    [DllImport("StockfishLib")]
    private static extern void EvalCpp();


    // Declare the delegates and events
    public delegate void StockfishCallbackDelegate(string message);

    public delegate void StockfishOutputDelegate(string output);

    public delegate void StockfishGoInfoDelegate(List<GoInfo> goInfos);
    public delegate void StockfishBestMoveDelegate(string bestMove);

    /// <summary>
    /// All the output from stockfish will go through here if you wish to handle all yourself
    /// </summary>
    public event StockfishOutputDelegate? StockfishOutput;
    /// <summary>
    /// Sends you the GoInfo objects that have been parsed from the Stockfish output
    /// </summary>
    public event StockfishGoInfoDelegate? GoInfoReceived;
    /// <summary>
    /// Sends you the UCI move that Stockfish deteremined to be best
    /// </summary>
    public event StockfishBestMoveDelegate? BestMoveReceived;

    static Stockfish? Current { get; set; }

    public Stockfish()
    {
        SetAppropriateDllDirectory();
        Current = this;
    }


    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    public static void SetAppropriateDllDirectory()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string architectureDir = GetArchitectureDirectory();

        // Construct the full path to the appropriate directory
        string dllDirectory = Path.Combine(baseDir, "Libraries", architectureDir);

        Console.WriteLine(dllDirectory);
        // Set that directory to be used for the subsequent DllImport calls
        SetDllDirectory(dllDirectory);
    }

    private static string GetArchitectureDirectory()
    {
        switch (RuntimeInformation.ProcessArchitecture)
        {
            case Architecture.X64:
                return "x64";
            case Architecture.X86:
                return "x86";
            case Architecture.Arm:
                return "ARM";
            case Architecture.Arm64:
                // Differentiate between ARM64 and Apple Silicon
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return "AppleSilicon";

                return "ARM64";
            default:
                throw new NotSupportedException("This architecture is not supported!");
        }
    }

    /// <summary>
    /// Runs all the initialization to start up the Stockfish Engine. Sets IsLibrary to true
    /// </summary>
    /// <param name="evalFile">
    /// Default uses the file included, otherwise use full path to your NNUE file
    /// </param>
    /// <param name="hashSizeInMB">
    /// Sets the size of the hash, in MB
    /// </param>
    /// <param name="multiPV">
    /// The number of lines for the engine to analyze and score
    /// </param>
    /// <param name="showWDL">
    /// Option to include Win, Draw, Loss odds in output and GoInfo
    /// </param>
    /// <param name="threads">
    /// Number of threads to use, use number of processors for max performance
    /// </param>
    public void Initialize(int hashSizeInMB = 512, int threads = 4, int multiPV = 3, bool showWDL = true, string evalFile = "NNUE/nn-0000000000a0.nnue")
    {
        Current = this;
        InitializeOutput();
        IsReadyCpp();
        InitializeEngineCpp();

        SetOptionCpp(new StringBuilder("EvalFile"), new StringBuilder(evalFile));
        SetOptionCpp(new StringBuilder("Threads"), new StringBuilder(threads.ToString()));
        SetOptionCpp(new StringBuilder("Hash"), new StringBuilder(hashSizeInMB.ToString()));
        SetOptionCpp(new StringBuilder("MultiPV"), new StringBuilder(multiPV.ToString()));
        SetOptionCpp(new StringBuilder("UCI_ShowWDL"), new StringBuilder(showWDL ? "true" : "false"));
    }

    /// <summary>
    /// Set the specified option to the specified value
    /// </summary>
    /// <param name="option">one of the standard UCI option names</param>
    /// <param name="value">the value to set the specified option to</param>
    public void SetOption(string option, string value)
    {
        SetOptionCpp(new StringBuilder(option), new StringBuilder(value));
    }

    /// <summary>
    /// Set the specified UCIOption enum to the specified value. Exceptions thrown if value is invalid.
    /// </summary>
    /// <param name="option">UCIOption enum that represents all the standard UCI Options</param>
    /// <param name="value">Use bool, int, or string values</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="FileNotFoundException"></exception>
    public void SetOption(UCIOption option, object value)
    {
        if (value == null)
            throw new ArgumentNullException("value");
        string valueString = value.ToString() ?? "";
        
        switch(option) 
        {
            case UCIOption.Threads:
                if (int.TryParse(valueString, out int threads))
                {
                    if (threads > 0 && threads <= 1024)
                        SetOption("Threads", threads.ToString());
                    else
                        throw new ArgumentOutOfRangeException("Threads value", "Must be greater than 0 and less than or equal to 1024");
                }
                else
                {
                    throw new ArgumentException("Must be integer for Threads", nameof(value));
                }
                break;
            case UCIOption.Hash:
                if (int.TryParse(valueString, out int hashSizeInMb))
                {
                    if (hashSizeInMb > 0 && hashSizeInMb <= 33554432)
                        SetOption("Hash", hashSizeInMb.ToString());
                    else
                        throw new ArgumentOutOfRangeException("Hash value", "Must be greater than 0 and less than or equal to 33554432 MB");
                }
                break;
            case UCIOption.ClearHash:
                SetOption("Clear Hash", "");
                break;
            case UCIOption.Ponder:
                if (bool.TryParse(valueString, out bool ponder))
                {
                    SetOption("Ponder", ponder ? "true" : "false");
                }
                else
                {
                    throw new ArgumentException("Must be true or false ", nameof(value));
                }
                break;
            case UCIOption.MultiPV:
                if (int.TryParse(valueString, out int  multiPV))
                {
                    if (multiPV > 0 && multiPV <= 500)
                    {
                        SetOption("MultiPV", multiPV.ToString());
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException("MultiPV value", "Must be greater than 0 and less than or equal to 500");
                    }
                }
                break;
            case UCIOption.EvalFile:
                if (File.Exists(valueString))
                {
                    SetOption("EvalFile", valueString);
                }
                else
                {
                    throw new FileNotFoundException(valueString);
                }
                break;
            case UCIOption.UCI_Chess960:
                if (bool.TryParse(valueString, out bool chess960))
                {
                    SetOption("UCI_Chess960", chess960 ? "true" : "false");
                }
                else
                {
                    throw new ArgumentException("Must be true or false ", nameof(value));
                }
                break;
            case UCIOption.UCI_ShowWDL:
                if (bool.TryParse(valueString, out bool showWDL))
                {
                    SetOption("UCI_ShowWDL", showWDL ? "true" : "false");
                }
                else
                {
                    throw new ArgumentException("Must be true or false ", nameof(value));
                }
                break;
            case UCIOption.UCI_LimitStrength:
                if (bool.TryParse(valueString, out bool limitStrength))
                {
                    SetOption("UCI_LimitStrength", limitStrength ? "true" : "false");
                }
                else
                {
                    throw new ArgumentException("Must be true or false ", nameof(value));
                }
                break;
            case UCIOption.UCI_Elo:
                if (int.TryParse(valueString, out int elo))
                {
                    if (elo >= 1320 && elo <= 3190)
                    {
                        SetOption("UCI_Elo", elo.ToString());
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException("UCI_Elo value", "Must be greater than or equal to 1320 and less than or equal to 3190");
                    }
                }
                break;
            case UCIOption.Skill_Level:
                if (int.TryParse(valueString, out int skillLevel))
                {
                    if (skillLevel >= 0 && skillLevel <= 20)
                    {
                        SetOption("Skill Level", skillLevel.ToString());
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException("Skill level value", "Must be greater than or equal to 0 and less than or equal to 20");
                    }
                }
                break;
            case UCIOption.SyzygyPath:
                if (Directory.Exists(valueString))
                {
                    SetOption("SyzygyPath", valueString);
                }
                else
                {
                    throw new DirectoryNotFoundException(valueString);
                }
                break;
            case UCIOption.SyzygyProbeDepth:
                if (int.TryParse(valueString, out int probeDepth))
                {
                    if (probeDepth > 0 && probeDepth <= 100)
                    {
                        SetOption("SyzygyProbeDepth", probeDepth.ToString());
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException("Probe Depth value", "Must be greater than 0 and less than or equal to 100");
                    }
                }
                break;
            case UCIOption.Syzygy50MoveRule:
                if (bool.TryParse(valueString, out bool moveRule))
                {
                    SetOption("Syzygy50MoveRule", moveRule ? "true" : "false");
                }
                else
                {
                    throw new ArgumentException("Must be true or false ", nameof(value));
                }
                break;
            case UCIOption.SyzygyProbeLimit:
                if (int.TryParse(valueString, out int probeLimit))
                {
                    if (probeLimit >= 0 && probeLimit <= 7)
                    {
                        SetOption("SyzygyProbeLimit", probeLimit.ToString());
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException("Probe Limit value", "Must be greater than or equal to 0 and less than or equal to 7");
                    }
                }
                break;
            case UCIOption.Move_Overhead:
                if (int.TryParse(valueString, out int overhead))
                {
                    if (overhead >= 0 && overhead <= 5000)
                    {
                        SetOption("Move Overhead", overhead.ToString());
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException("Move overhead value", "Must be greater than or equal to 0 and less than or equal to 5000");
                    }
                }
                break;
            case UCIOption.Slow_Mover:
                if (int.TryParse(valueString, out int slowMover))
                {
                    if (slowMover >= 10 && slowMover <= 1000)
                    {
                        SetOption("Slow Mover", slowMover.ToString());
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException("Slow Mover value", "Must be greater than or equal to 10 and less than or equal to 1000");
                    }
                }
                break;
            case UCIOption.NodesTime:
                if (int.TryParse(valueString, out int nodesTime))
                {
                    if (nodesTime >= 0 && nodesTime <= 10000)
                    {
                        SetOption("nodestime", nodesTime.ToString());
                    }
                    else
                    {
                        throw new ArgumentOutOfRangeException("NodesTime value", "Must be greater than or equal to 0 and less than or equal to 10000");
                    }
                }
                break;
            case UCIOption.Debug_Log_File:
                if (!File.Exists(valueString))
                {
                    File.Create(valueString);                  
                }
                if (File.Exists(valueString))
                {
                    SetOption("Debug Log File", valueString);
                }
                else
                {
                    throw new FileNotFoundException(valueString);
                }
                break;
        }
    }

    bool allStockfish = false;

    /// <summary>
    /// Sets the position with "position" command with FEN board position and UCI moves
    /// </summary>
    /// <param name="fenString">Chess board position in FEN format or "startpos"</param>
    /// <param name="moves">UCI moves separated with a space</param>
    public void SetPosition(string fenString, string moves)
    {
        string parameters = fenString == "startpos" ? "startpos" : "fen " + fenString;
        if (!string.IsNullOrEmpty(moves))
        {
            parameters += " moves " + moves;
        }
        SetPosition(parameters);
    }

    /// <summary>
    /// Raw "position" command in stockfish
    /// </summary>
    /// <param name="parameters">the string of parameters that come after "position" in traditional CL Stockfish</param>
    public void SetPosition(string parameters)
    {
        StringBuilder sb = new StringBuilder(parameters);
        SetPositionCpp(sb);
    }


    /// <summary>
    /// Initiates engine analysis to specified parameters. Must pick one of these: depth, infinite, mate, nodes
    /// All other parameters can be added in addition
    /// </summary>
    /// <param name="depth">Depth of moves to search</param>
    /// <param name="infinite">search to infinite depth</param>
    /// <param name="moves">UCI string of current possible moves to analyze</param>
    /// <param name="mate">search for mate to specified depth</param>
    /// <param name="nodes">search based on number of nodes</param>
    /// <param name="movetime">time to conduct the search, in ms</param>
    /// <param name="wtime">white time to move, in ms</param>
    /// <param name="btime">black time to move, in ms</param>
    /// <param name="winc">white time increment, in ms</param>
    /// <param name="binc">black time increment, in ms</param>
    /// <param name="movesToGo">moves until next time control</param>
    /// <param name="perft">A debugging function to walk the move generation tree of strictly legal moves to count all the leaf nodes of a certain depth</param>
    public void Go(int depth = 10, bool infinite = false, string moves = "", bool mate = false, int nodes = -1, int movetime = -1, int wtime = -1, int btime = -1, int winc = -1, int binc = -1, int movesToGo = -1, int perft = -1)
    {
        string parameters = infinite ? "infinite" : "depth " + depth.ToString();
        if (mate)
        {
            parameters = "mate " + depth.ToString();
        }
        if (nodes > 0)
        {
            parameters = "nodes " + nodes.ToString();
        }
        if (!string.IsNullOrEmpty(moves))
        {
            parameters += " moves " + moves;
        }
        if (movetime > 0)
        {
            parameters += " movetime " + movetime.ToString();
        }
        if (wtime > 0) 
        {
            parameters += " wtime " + wtime.ToString();
        }
        if (btime > 0)
        {
            parameters += " btime " + btime.ToString();
        }
        if (winc > 0)
        {
            parameters += " winc " + winc.ToString();
        }
        if (binc > 0)
        {
            parameters += " binc " + binc.ToString();
        }
        if (movesToGo > 0)
        {
            parameters += " movestogo " + movesToGo.ToString();
        }
        if (perft > 0)
        {
            parameters += " perft " + perft.ToString();
        }
        Go(parameters);
    }

    /// <summary>
    /// Same as Go, but must use depth
    /// </summary>
    /// <param name="depth">Depth of moves to search</param>
    /// <param name="moves">UCI string of current possible moves to analyze</param>
    /// <param name="movetime">time to conduct the search, in ms</param>
    /// <param name="wtime">white time to move, in ms</param>
    /// <param name="btime">black time to move, in ms</param>
    /// <param name="winc">white time increment, in ms</param>
    /// <param name="binc">black time increment, in ms</param>
    /// <param name="movesToGo">moves until next time control</param>
    /// <param name="perft">A debugging function to walk the move generation tree of strictly legal moves to count all the leaf nodes of a certain depth</param>
    public void GoDepth(int depth, string moves = "", int movetime = -1, int wtime = -1, int btime = -1, int winc = -1, int binc = -1, int movesToGo = -1, int perft = -1)
    {
        Go(depth, false, moves, false, -1, movetime, wtime, btime, winc, binc, movesToGo, perft);
    }

    /// <summary>
    /// Same as Go, but must be infinte. Must use Stop or Quit to end search or set movetime
    /// </summary>
    /// <param name="moves">UCI string of current possible moves to analyze</param>
    /// <param name="movetime">time to conduct the search, in ms</param>
    /// <param name="wtime">white time to move, in ms</param>
    /// <param name="btime">black time to move, in ms</param>
    /// <param name="winc">white time increment, in ms</param>
    /// <param name="binc">black time increment, in ms</param>
    /// <param name="movesToGo">moves until next time control</param>
    /// <param name="perft">A debugging function to walk the move generation tree of strictly legal moves to count all the leaf nodes of a certain depth</param>
    public void GoInfinite(string moves = "", int movetime = -1, int wtime = -1, int btime = -1, int winc = -1, int binc = -1, int movesToGo = -1, int perft = -1)
    {
        Go(10, true, moves, false, -1, movetime, wtime, btime, winc, binc, movesToGo, perft);
    }

    /// <summary>
    /// Same as Go but will search for mate to the specified depth
    /// </summary>
    /// <param name="depth">Depth of moves to search</param>
    /// <param name="moves">UCI string of current possible moves to analyze</param>
    /// <param name="movetime">time to conduct the search, in ms</param>
    /// <param name="wtime">white time to move, in ms</param>
    /// <param name="btime">black time to move, in ms</param>
    /// <param name="winc">white time increment, in ms</param>
    /// <param name="binc">black time increment, in ms</param>
    /// <param name="movesToGo">moves until next time control</param>
    /// <param name="perft">A debugging function to walk the move generation tree of strictly legal moves to count all the leaf nodes of a certain depth</param>
    public void GoMate(int depth = 10, string moves = "", int movetime = -1, int wtime = -1, int btime = -1, int winc = -1, int binc = -1, int movesToGo = -1, int perft = -1)
    {
        Go(depth, false, moves, true, -1, movetime, wtime, btime, winc, binc, movesToGo, perft);
    }

    /// <summary>
    /// Same as Go but must use Nodes
    /// </summary>
    /// <param name="moves">UCI string of current possible moves to analyze</param>
    /// <param name="nodes">search based on number of nodes</param>
    /// <param name="movetime">time to conduct the search, in ms</param>
    /// <param name="wtime">white time to move, in ms</param>
    /// <param name="btime">black time to move, in ms</param>
    /// <param name="winc">white time increment, in ms</param>
    /// <param name="binc">black time increment, in ms</param>
    /// <param name="movesToGo">moves until next time control</param>
    /// <param name="perft">A debugging function to walk the move generation tree of strictly legal moves to count all the leaf nodes of a certain depth</param>
    public void GoNodes(int nodes, string moves = "", int movetime = -1, int wtime = -1, int btime = -1, int winc = -1, int binc = -1, int movesToGo = -1, int perft = -1)
    {
        Go(10, false, moves, false, nodes, movetime, wtime, btime, winc, binc, movesToGo, perft);
    }

    /// <summary>
    /// Raw "go" command from Stockfish, add parameters manually with CL-like input
    /// </summary>
    /// <param name="parameters">The parameters that come after "go" when using CL ie "depth 10"</param>
    public void Go(string parameters)
    {
        GoCpp(new StringBuilder(parameters));
    }

    /// <summary>
    /// Tells the engine to use UCI commands (this should be called primarily for testing engine is ready)
    /// Look for UCIOk event to be triggerd that this worked
    /// </summary>
    public void UCI()
    {
        UCICpp();
    }

    /// <summary>
    /// Lets engine know that you are going to change the game that is analyzed and clears current analysis
    /// </summary>
    public void UciNewGame()
    {
        UcinewgameCpp();
    }

    /// <summary>
    /// Will trigger ReadyOk event if Ready
    /// </summary>
    public void IsReady()
    {
        IsReadyCpp();
    }


    /// <summary>
    /// Stops all threads that are analyzing
    /// </summary>
    public void Quit()
    {
        QuitCpp();
    }

    /// <summary>
    /// Stops all threads that are analyzing, should trigger BestMoveReceived event from current progress
    /// </summary>
    public void Stop()
    {
        StopCpp();
    }

    /// <summary>
    /// Tells engine the ponder move was hit and can continue analyzing from that move
    /// </summary>
    public void PonderHit()
    {
        PonderHitCpp();
    }

    /// <summary>
    /// Flips the side to move
    /// </summary>
    public void Flip()
    {
        FlipCpp();
    }
    /// <summary>
    /// Runs a benchmark on pre-selected positions. Non-standard command, see Stockfish wiki
    /// </summary>
    /// <param name="parameters">Parameters that come after the "bench" command. See Stockfish wiki</param>
    public void Bench(string parameters)
    {
        BenchCpp(new StringBuilder(parameters));
    }
    /// <summary>
    /// Sends an ASCII art drawing of board to ouput as well as the FEN board position
    /// </summary>
    public void D()
    {
        DCpp();
    }
    /// <summary>
    /// Generates a static evaluation of the current board position
    /// </summary>
    public void Eval()
    {
        EvalCpp();
    }

    /// <summary>
    /// Sets the fact that Stockfish needs to run as a library and communicate externally, set to false if you just want to get the standard Console output
    /// </summary>
    /// <param name="isLibrary"></param>
    public void SetIsLibrary(bool isLibrary)
    {
        SetIsLibraryCpp(isLibrary);
    }

    // Declare the callback function.
    public static void StockfishCallback(string message)
    {
        HandleStockfishOutput(message);
    }


    /// <summary>
    /// Registers our c# callback in the c++ library
    /// </summary>
    public static void InitializeOutput()
    {
        RegisterCallback(StockfishCallback);
    }


    private static List<GoInfo> CurrentGoInfos = new List<GoInfo>();

    public static void HandleStockfishOutput(string stockfishOutput)
    {
        Current?.StockfishOutput?.Invoke(stockfishOutput);
        stockfishOutput = stockfishOutput.Trim();
        string token = stockfishOutput.Split(' ')[0];

        switch (token)
        {
            case "info":
                if (!stockfishOutput.Contains("currmove"))
                {
                    var goInfos = ParseGoInfos(stockfishOutput);
                    Current?.GoInfoReceived?.Invoke(goInfos);
                    // Handle the parsed GoInfo object as needed
                }
                break;
            case "bestmove":
                string bestMove = ParseBestMove(stockfishOutput);
                Current?.BestMoveReceived?.Invoke(bestMove);
                break;
            case "ponder":
                // Handle ponder token
                break;
            default:
                // Handle other cases or unknown tokens
                break;
        }
    }

    public static List<GoInfo> ParseGoInfos(string input)
    {
        var goInfoList = new List<GoInfo>();

        // Split the input string based on the word "info"
        var segments = input.Split(new[] { "info" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            // Append "info" to the beginning of the segment to match the original format
            var line = "info" + segment;

            // Call the original ParseGoInfo code on the line
            var goInfo = ParseGoInfo(line);
            //Debug.Log(goInfo.ToString());
            // Add the result to the list
            goInfoList.Add(goInfo);
        }

        return goInfoList;
    }
    public static GoInfo ParseGoInfo(string input)
    {
        var goInfo = new GoInfo();

        // Extract MultiPV
        var multiPvMatch = Regex.Match(input, @"multipv (\d+)");
        if (multiPvMatch.Success)
        {
            goInfo.MultiPV = int.Parse(multiPvMatch.Groups[1].Value);
        }

        // Extract Score (CP or Mate)
        var scoreMatch = Regex.Match(input, @"score (cp|mate) (-?\d+)");
        if (scoreMatch.Success)
        {
            goInfo.IsMate = scoreMatch.Groups[1].Value == "mate";
            goInfo.Score = int.Parse(scoreMatch.Groups[2].Value);
        }

        // Extract WDL
        var wdlMatch = Regex.Match(input, @"wdl (\d+) (\d+) (\d+)");
        if (wdlMatch.Success)
        {
            goInfo.WDL = (int.Parse(wdlMatch.Groups[1].Value),
                          int.Parse(wdlMatch.Groups[2].Value),
                          int.Parse(wdlMatch.Groups[3].Value));
        }

        // Extract Depth
        var depthMatch = Regex.Match(input, @"depth (\d+)");
        if (depthMatch.Success)
        {
            goInfo.Depth = int.Parse(depthMatch.Groups[1].Value);
        }

        // Extract SelDepth
        var selDepthMatch = Regex.Match(input, @"seldepth (\d+)");
        if (selDepthMatch.Success)
        {
            goInfo.SelDepth = int.Parse(selDepthMatch.Groups[1].Value);
        }

        // Extract Time
        var timeMatch = Regex.Match(input, @"time (\d+)");
        if (timeMatch.Success)
        {
            goInfo.Time = int.Parse(timeMatch.Groups[1].Value);
        }

        // Extract PV (Principal Variation)
        var pvMatch = Regex.Match(input, @" pv ([a-h1-8\s]+)");
        if (pvMatch.Success)
        {
            goInfo.Moves = pvMatch.Groups[1].Value.Trim().Split(' ');
        }

        return goInfo;
    }
    public static string ParseBestMove(string output)
    {
        // Define a regular expression pattern to match the best move
        string pattern = @"bestmove\s+(\S+)";
        Match match = Regex.Match(output, pattern);

        // If a match is found, return the best move
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        // If no match is found, return null or an appropriate default value
        return "";
    }

}

public enum UCIOption
{
    /// <summary>
    /// Set number of threads from 1 to 1024
    /// </summary>
    Threads,
    /// <summary>
    /// Set hash size in MB up to 33554432 MB
    /// </summary>
    Hash,
    /// <summary>
    /// Clears the hash
    /// </summary>
    ClearHash,
    /// <summary>
    /// true/false Tells the engine to ponder the next move while awaiting opponent to move
    /// </summary>
    Ponder,
    /// <summary>
    /// Sets number of lines to analyze from 1 up to 500
    /// </summary>
    MultiPV,
    /// <summary>
    /// Set your own NNUE network file, use full file path
    /// </summary>
    EvalFile,
    /// <summary>
    /// Currently does nothing in Stockfish
    /// </summary>
    UCI_AnalyseMode,
    /// <summary>
    /// true/false are we playing Chess960
    /// </summary>
    UCI_Chess960,
    /// <summary>
    /// true/false Show WDL stats in go output
    /// </summary>
    UCI_ShowWDL,
    /// <summary>
    /// true/false enable weaker play with UCI_Elo, overrides Skill Level option
    /// </summary>
    UCI_LimitStrength,
    /// <summary>
    /// Sets the Elo the engine plays at from 1320 to 3190
    /// </summary>
    UCI_Elo,
    /// <summary>
    /// Set Skill Level from 0 to 20
    /// </summary>
    Skill_Level,
    /// <summary>
    /// Path to the folders/directories storing the Syzygy tablebase files. 
    /// Multiple directories are to be separated by ; on Windows 
    /// and by : on Unix-based operating systems. 
    /// Do not use spaces around the ; or :.
    /// </summary>
    SyzygyPath,
    /// <summary>
    /// Minimum remaining search depth for which a position is probed from 1 to 100
    /// </summary>
    SyzygyProbeDepth,
    /// <summary>
    /// true/false disable to allow games that break the 50 move rule to end in win/loss
    /// </summary>
    Syzygy50MoveRule,
    /// <summary>
    /// Limit Syzygy tablebase probing to positions with at most this many pieces left from 0 to 7
    /// </summary>
    SyzygyProbeLimit,
    /// <summary>
    /// Assume a time delay of x ms (from 0 to 5000) due to network and GUI overheads
    /// </summary>
    Move_Overhead,
    /// <summary>
    /// 10 to 1000; Lower values will make Stockfish take less time in games, higher values will make it think longer
    /// </summary>
    Slow_Mover,
    /// <summary>
    /// Primarily for engine testing, set from 0 to 10000 to use nodes searched instead of wall time to account for elapsed time
    /// </summary>
    NodesTime,
    /// <summary>
    /// Specify a text file to write all communication to and from engine into a text file
    /// </summary>
    Debug_Log_File,
}

/// <summary>
/// The class that represents the info output triggered by the Go command
/// </summary>
public class GoInfo
{
    /// <summary>
    /// number of lines to analyze
    /// </summary>
    public int MultiPV { get; set; }
    /// <summary>
    /// Depth that has been searched
    /// </summary>
    public int Depth { get; set; }
    /// <summary>
    /// Maximum depth reached at any point in search
    /// </summary>
    public int SelDepth { get; set; }
    /// <summary>
    /// Time to get this output
    /// </summary>
    public int Time { get; set; }
    /// <summary>
    /// Moves in UCI format in an array of string
    /// </summary>
    public string[] Moves { get; set; } = new string[0];
    /// <summary>
    /// Score of move, in cp (centipawns)
    /// </summary>
    public int Score { get; set; }
    /// <summary>
    /// Odds * 1000 of Win, Draw, Loss
    /// </summary>
    public (int W, int D, int L) WDL { get; set; }
    /// <summary>
    /// If contains mate
    /// </summary>
    public bool IsMate { get; set; }
    /// <summary>
    /// Number of moves for mate
    /// </summary>
    public int MateIn { get; set; }

    public override string ToString()
    {
        string moves = string.Join(" ", Moves ?? Array.Empty<string>());
        if (IsMate)
        {
            return $"MultiPV: {MultiPV}, Depth: {Depth}, SelDepth: {SelDepth}, Time: {Time}, Moves: {moves}, Mate in: {MateIn}";
        }
        else
        {
            return $"MultiPV: {MultiPV}, ScoreCp: {Score}, Depth: {Depth}, SelDepth: {SelDepth}, Time: {Time}, Moves: {moves}, WDL: {WDL.W} {WDL.D} {WDL.L}";
        }
    }
}
