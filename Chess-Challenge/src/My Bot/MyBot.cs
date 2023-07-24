using System;
using ChessChallenge.API;
using System.Numerics;
using System.Collections.Generic;

public class MyBot : IChessBot
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

    // Piece values: pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 100, 300, 350, 550, 900, 0};
    // used for king safety. I cba explain it
    int[] attackWeights = { 0, 25, 50, 75, 88, 94, 97, 99, 99, 99, 99, 99, 99, 99, 99 };

    static Board board;
    Timer timer;
    
    static int NEGATIVE_INFINITY = -99999999;
    static int POSITIVE_INFINITY = 99999999;
    static int CHECKMATE_EVAL = -9999999;

    private static ulong aFile = 0x8080808080808080;

    public Move Think(Board pboard, Timer ptimer)
    {
        board = pboard;
        timer = ptimer;
        MoveValue bestMoveValue = new ();
        MoveValue bestMoveValueThisIteration = new();
        int depth = 0;
        while (!ShouldFinishSearch())
        {
            bestMoveValue = bestMoveValueThisIteration;
            depth++;
            bestMoveValueThisIteration = NegaMax(depth, NEGATIVE_INFINITY, POSITIVE_INFINITY);
        }
        Console.WriteLine("Depth: " + depth);
        Console.WriteLine("Eval: " + bestMoveValue.value);
        return bestMoveValue.move;
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
            (6-DistanceFromEdgeOfBoard(square)) * PopCount(board.AllPiecesBitboard) + 
            DistanceFromEdgeOfBoard(square) * (32-PopCount(board.AllPiecesBitboard)) / 3
    };

    MoveValue NegaMax(int depth, int alpha, int beta)
    {
        Move bestMove = Move.NullMove;

        int currentEval = 0;

        if (depth <= 0) // quiescence
        {
            currentEval = EvaluateBoard();
            if (currentEval >= beta) return new (bestMove, beta);
            if (currentEval > alpha) alpha = currentEval;
        }
        

        var moves = board.GetLegalMoves(depth<=0);


        if (moves.Length == 0)
        {
            if (depth <= 0) return new (bestMove, currentEval);
            if (board.IsInCheck()) return new(bestMove, CHECKMATE_EVAL - depth);
            return new(bestMove, -50);
        }

        SortMoves(ref moves);

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int newEval = -NegaMax(depth - 1, -beta, -alpha).value;
            board.UndoMove(move);

            if (ShouldFinishSearch()) break;

            if (newEval >= beta) return new(move, beta);
            
            if (newEval > alpha)
            {
                bestMove = move;
                alpha = newEval;
            }
        }
        return new (bestMove, alpha);
    }

    int EvaluateBoard()
    {
        // sum piece
        int res = 0;
        var pieceLists = board.GetAllPieceLists();
        for (int pieceType = 0; pieceType < 6; pieceType++)
        {
            // PIECE SQUARE VALUE SUMS
            res += (pieceLists[pieceType].Count - pieceLists[pieceType + 6].Count) * pieceValues[pieceType];

            // PIECE SQUARE ESTIMATES
            ulong friendlyBB = board.GetPieceBitboard((PieceType)pieceType + 1, true);
            ulong enemyBB = board.GetPieceBitboard((PieceType)pieceType + 1, false);
            while (friendlyBB > 0)
                res += pieceSquareEstimaters[pieceType](
                       new Square(PopLsb(ref friendlyBB)));
            while (enemyBB > 0)
                res -= pieceSquareEstimaters[pieceType](
                    new Square(PopLsb(ref enemyBB) ^ 56)); // xor with 56 flips the index of the square to treat it as if it was for the other team
        }


        if (!board.IsWhiteToMove) res = -res;

        res += EvaluateColour(board.IsWhiteToMove) - EvaluateColour(!board.IsWhiteToMove);

        return res;
        
    }
    int EvaluateColour(bool isWhite)
    {
        int res = 0;
        ulong friendlyPawnBB = board.GetPieceBitboard(PieceType.Pawn, isWhite);
        ulong enemyPawnBB = board.GetPieceBitboard(PieceType.Pawn, !isWhite);
        
        ulong friendlyBB = isWhite ? board.WhitePiecesBitboard : board.BlackPiecesBitboard;
        ulong enemyBB = isWhite ? board.BlackPiecesBitboard : board.WhitePiecesBitboard;
        int enemyCount = PopCount(enemyBB);
        // KING SAFETY
        ulong kingBB = board.GetPieceBitboard(PieceType.King, isWhite);
        int kingPos = BitboardHelper.ClearAndGetIndexOfLSB(ref kingBB);
        ulong kingQuadrantBitboard = getQuadrantBitboard(kingPos);
        
        int attackingPiecesCount = PopCount(enemyBB ^ enemyPawnBB ^ board.GetPieceBitboard(PieceType.King, !isWhite) & kingQuadrantBitboard);
        // bad if there are more enemies in the king's quadrant than friendies
        res -= attackWeights[Math.Min(7, attackingPiecesCount)] / 5;
        
        res -= PopCount(BitboardHelper.GetSliderAttacks(PieceType.Queen, new (kingPos), board)) * enemyCount / 16;
        // BISHOP PAIR
        if (PopCount(board.GetPieceBitboard(PieceType.Bishop, isWhite)) >= 2) res += 30;
        
        // PAWN STRUCTURE
        //foreach (int index in IterBitboard(friendlyPawnBB))
        //{
            
        //}
        // ROOK OPEN FILES
        
        return res;
    }

    int GetPieceValue(Square sq)
    {
        return pieceValues[(int)board.GetPiece(sq).PieceType - 1];
    }

    ulong getFileBitboard(int pos) => aFile >> (pos % 8);

    void SortMoves(ref Move[] moves)
    {

        var moveScores = new int[moves.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];
            if (move.IsCapture)
            {
                moveScores[i] += GetPieceValue(move.TargetSquare) - GetPieceValue(move.StartSquare) / 10;

            }
            
            // negate so that the moves get sorted best to worst
            moveScores[i] *= -1;
        }
        
        Array.Sort(moveScores, moves);
    }

    int PopLsb(ref ulong bitmap) => BitboardHelper.ClearAndGetIndexOfLSB(ref bitmap);

    static int PopCount(ulong bitboard) => BitOperations.PopCount(bitboard);

    ulong getQuadrantBitboard(int index)
    {
        ulong bb = BitboardHelper.GetKingAttacks(new (index));
        if (board.IsWhiteToMove) return bb | (bb >> 8) | (bb >> 16);
        else return bb | (bb << 8) | (bb << 16);
    }
    
    bool ShouldFinishSearch()
    {
        return timer.MillisecondsElapsedThisTurn > 100;
    }
    
    
}