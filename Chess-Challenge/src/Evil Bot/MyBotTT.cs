// 970 tokens

using System;
using ChessChallenge.API;
using System.Numerics;
using System.Collections.Generic;

public class MyBotTT : IChessBot
{
    struct MoveValue
    {
        public MoveValue(Move m, int v)
        {
            move = m;
            value = v;
        }

        public Move move;
        public int value;
    }

    enum NodeType
    {
        EXACT, LOWER, UPPER
    }
    struct TTEntry
    {
        public readonly ulong zobrist;
        public readonly int depth;
        public readonly MoveValue moveValue;
        public readonly NodeType nodeType;
        public TTEntry(ulong zob, int plyFromRoot, MoveValue moveEval, NodeType type)
        {
            zobrist = zob;
            depth = plyFromRoot;
            moveValue = moveEval;
            nodeType = type;
        }
    }

    
    /// Piece values: pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 100, 300, 350, 550, 900, 0};

    static Board board;
    Timer timer;
    
    static int NEGATIVE_INFINITY = -99999999;
    static int POSITIVE_INFINITY = 99999999;
    static int CHECKMATE_EVAL = -9999999;
    private int rootDepth = 0;
    static TTEntry[] TT;
    private TTEntry LookupFailed = new (0, NEGATIVE_INFINITY, new (), NodeType.EXACT);
    private int TThits = 0;

    public MyBotTT()
    {
        TT = new TTEntry[1000000];
        for (int i = 0; i < TT.Length; i++) TT[i] = LookupFailed;
    }
    
    public Move Think(Board pboard, Timer ptimer)
    {
        TThits = 0;
        board = pboard;
        timer = ptimer;
        Move bestMove = Move.NullMove;
        Move bestMoveThisIteration = Move.NullMove;
        rootDepth = 0;
        while (!ShouldFinishSearch())
        {
            bestMove = bestMoveThisIteration;
            rootDepth++;
            bestMoveThisIteration = NegaMax(rootDepth, NEGATIVE_INFINITY, POSITIVE_INFINITY).move;
        }
        Console.WriteLine("Depth: " + rootDepth);
        Console.WriteLine("TT hits: " + TThits);
        return bestMove;
    }

    static int DistanceFromEdgeOfBoard(int x)
    {
        return Math.Min(7 - x, x);
    }

    static int DistanceFromEdgeOfBoard(Square square) 
    {
        return DistanceFromEdgeOfBoard(square.File) + DistanceFromEdgeOfBoard(square.Rank);
    }

    // functions that attempt to simulate a piece square table
    private static Func<Square,  int>[] pieceSquareEstimaters = {
        square =>  // PAWN
            square.Rank > 1 ? (square.Rank - 2) * 10 : DistanceFromEdgeOfBoard(square.File) == 3 ? -20 : 10,
        square =>  // KNIGHT
            DistanceFromEdgeOfBoard(square) * 10 - 40,
        square =>  // BISHOP
            pieceSquareEstimaters[1](square),
        square =>  // ROOK
            square.Rank == 6 ? 10 : square.File % 7 != 0 ? 0 : square.Rank == 0 ? 0 : -5,
        square =>  // QUEEN
            pieceSquareEstimaters[1](square),
        square => // KING
            (6-DistanceFromEdgeOfBoard(square)) * BitOperations.PopCount(board.AllPiecesBitboard) + 
                    DistanceFromEdgeOfBoard(square) * (32-BitOperations.PopCount(board.AllPiecesBitboard)) / 3
    };

    MoveValue NegaMax(int depth, int alpha, int beta)
    {
        
        // check if transpos table contains our current state
        ulong TTind = board.ZobristKey % (ulong)TT.Length;
        TTEntry entry = TT[TTind];
        if (entry.zobrist == board.ZobristKey && entry.depth >= depth &&
            ((entry.nodeType == NodeType.LOWER && entry.moveValue.value >= beta) ||
             (entry.nodeType == NodeType.UPPER && entry.moveValue.value <= alpha)))
        {

            if (entry.moveValue.move != Move.NullMove)
            {
                TThits++;
                return entry.moveValue;
            }
                
        }

        Move bestMove = Move.NullMove;
        if (board.IsInCheckmate()) return new (bestMove, CHECKMATE_EVAL - depth);
        if (board.IsDraw()) return new (bestMove, -50);
        

        if (depth <= 0)
        {
            int currentEval = EvaluateBoard();
            if (currentEval >= beta)
            {
                TT[TTind] = new (board.ZobristKey, depth, new (bestMove, alpha), NodeType.EXACT);
                return new (bestMove, beta);
            }
            if (currentEval > alpha) alpha = currentEval;
        }

        Span<Move> moves = new(new Move[256]);
        board.GetLegalMovesNonAlloc(ref moves, depth <= 0);
        
        sortMoves(ref moves);

        NodeType nodeType = NodeType.UPPER;
        
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int newEval = -NegaMax(depth - 1, -beta, -alpha).value;
            board.UndoMove(move);

            if (ShouldFinishSearch()) break;

            if (newEval >= beta)
            {
                nodeType = NodeType.LOWER;
                alpha = beta;
                bestMove = move;
                break;
            }

            if (newEval > alpha)
            {
                nodeType = NodeType.EXACT;
                bestMove = move;
                alpha = newEval;
            }
        }
        
        TT[TTind] = new (board.ZobristKey, depth, new MoveValue(bestMove, alpha), nodeType);

        return new (bestMove, alpha);
    }

    int EvaluateBoard()
    {
        return EvaluateColour(board.IsWhiteToMove) - EvaluateColour(!board.IsWhiteToMove);
    }
    int EvaluateColour(bool isWhite)
    {
        // sum piece
        int res = 0;
        for (int piece = 0; piece<6; piece++)
        {
            PieceType pieceType = (PieceType)piece + 1;
            // PIECE SQUARE VALUE SUMS
            res += board.GetPieceList(pieceType, isWhite).Count * pieceValues[piece];
            // PIECE SQUARE ESTIMATES
            ulong friendlyBB = board.GetPieceBitboard(pieceType, isWhite);
            foreach (int ind in IterBitboard(friendlyBB))
                res += pieceSquareEstimaters[piece](new (ind^ (isWhite? 0 : 56)));

        }
        return res;
    }

    int GetPieceValue(Square sq)
    {
        return pieceValues[(int)board.GetPiece(sq).PieceType - 1];
    }

    void sortMoves(ref Span<Move> moves)
    {

        Span<int> moveScores = new (new int[moves.Length]);
        Move hashMove = TT[board.ZobristKey % (ulong)TT.Length].moveValue.move;
        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];
            if (move.IsCapture)
            {
                moveScores[i] += GetPieceValue(move.TargetSquare) - GetPieceValue(move.StartSquare) / 10;
                if (move == hashMove) moveScores[i] += 10000;
            }
            
            // negate so that the moves get sorted best to worst
            moveScores[i] *= -1;
        }
        MemoryExtensions.Sort(moveScores, moves);
    }
    
    IEnumerable<int> IterBitboard(ulong bitmap)
    {
        while (bitmap > 0)
        {
            yield return BitboardHelper.ClearAndGetIndexOfLSB(ref bitmap);
        }
    }
    
    bool ShouldFinishSearch()
    {
        return timer.MillisecondsElapsedThisTurn > 100;
    }
    
    
}