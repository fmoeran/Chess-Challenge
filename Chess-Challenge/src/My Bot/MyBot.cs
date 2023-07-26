using System;
using ChessChallenge.API;
using System.Numerics;
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

    struct TTEntry
    {
        public ulong key;
        public MoveValue moveValue;
        public int depth,  bound;
        public TTEntry(ulong _key, MoveValue _moveValue, int _depth, int _bound)
        {
            key = _key; moveValue = _moveValue; depth = _depth;  bound = _bound;
        }
    }

    const int entries = (1 << 20);
    TTEntry[] tt = new TTEntry[entries];


    // Piece values: pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 100, 300, 350, 550, 900, 0}, attackWeights = { 0, 25, 50, 75, 88, 94, 97, 99, 99}, gamePhaseIncs = { 0, 1, 1, 2, 4, 0 };

    Board board;
    Timer timer;


    int NEGATIVE_INFINITY = -99999999, POSITIVE_INFINITY = 99999999, CHECKMATE_EVAL = -9999999, rootDepth;

    public Move Think(Board pboard, Timer ptimer)
    {
        board = pboard;
        timer = ptimer;
        MoveValue bestMoveValue = new ();
        rootDepth = 0;
        while (timer.MillisecondsElapsedThisTurn < 50)
        {
            rootDepth++;
            bestMoveValue = NegaMax(rootDepth, NEGATIVE_INFINITY, POSITIVE_INFINITY);
            
        }
        Console.WriteLine("Depth: " + rootDepth);
        Console.WriteLine("Eval: " + bestMoveValue.value);

        return bestMoveValue.move;
    }

    ulong[] whitePSQT = {
         0x007B16BA4B8F7B00, 0x0021DC89C2B04F00, 0x000D1BCDF7871B00, 0xFF1BCE1560B0B5FF, 0xFFEB0304667E43FF, 0xFF9DB385E7BF9EFF, 0xFFA28385E7BF9FFF, 0x007F7C7A18406000,
         0xCC717E504B0D3582, 0xA40B0F3DE35BDFC4, 0x5CE4794E75B97CB5, 0xC6D6C0A813C8C457, 0x2E4AB519820A1A00, 0xF2E45A21838BDDFB, 0x4CC32101838BDFFE, 0x103CFEFE7C742000,
         0x4F6DDEF1FEEEB3B8, 0xB3BCC6B4C8BE2336, 0x5CDBADCF41C03476, 0x6E1C214AC8A199FC, 0xEE80FEFBD181D942, 0xD72D81C3C18199BF, 0xFB8D81C3C18199FF, 0x00727E3C3E7E6600,
         0x9D4B043DAE0090E6, 0xE7570EC9BD121C03, 0xFF0AD305DF4ED2FA, 0x6DE3249C553D0767, 0xAA90296679715964, 0x303C41C7FEFE5EA7, 0x200001C7FFFFDFE7, 0xDFFFFE3800002018,
         0x5CA995D33A2AA91B, 0x50814950D549736F, 0x45D71F7FB29951B9, 0xA6DA5F8C40A3991A, 0xE28E4CD0FDBEDB8D, 0x133DAFDFFFBFDA7F, 0x031F0FDFFFBFDBFF, 0xFCE0F02000402400,
         0xEBEB060C97C8134D, 0x209E11F6922BCC92, 0x2C6328ED1FCBE231, 0x6B45FEC72857FB51, 0x707A2580BB18D71A, 0x48BEBD7F46E7E731, 0x79FEBDFFFFFFFF39, 0x86014200000000C6,
         0x00EAC437994BD900, 0x00F46031FEC18E00, 0x00E2A5D986660E00, 0xFFCD292B421B6EFF, 0xFFABE538FEFFEEFF, 0xFFD7DD38FEFFEEFF, 0xFFFF0238FEFFEEFF, 0x00FFFFC701001100,
         0xE6C81580C11D542F, 0x55FFFFC2C97F73A7, 0x2F6D1D413637584C, 0x4700CEDFC9D0A3B8, 0x829ABC4242369D42, 0xBC7F7FC3C3F77E3D, 0x7FFFFFC3C3F7FFFF, 0x0000003C3C080000,
         0x926E99A434D9DE05, 0xB9B99A7BEA6F3F17, 0xC1FE382B798FC9ED, 0x43ACE5EA32AD53A0, 0x3C57FFEBF36E3C5A, 0xFFFFFFEBF3EFFFFF, 0xFFFFFFEBF3EFFFFF, 0x000000140C100000,
         0x3F0617FD0839086A, 0x4AF0B0434D78B0ED, 0xC650EF4036AD8338, 0xC2E01FBB0FA20CE6, 0xC2F0FFFBFF5FFF5F, 0xC2F0FFFBFFFFFFFF, 0xC2F0FFFBFFFFFFFF, 0x3D0F000400000000,
         0x58042874A146A228, 0xF97EDD811D5466C0, 0xE04FC62056FB7A12, 0x5EE59F0F9BD959CC, 0x4188BEA968D84098, 0x4191875101DBBF77, 0x4181870101DBFFFF, 0xBE7E78FEFE240000,
         0x6F618D5C3F582DC6, 0x6F2519D9558B3463, 0x821B1300466AC172, 0xCE4165DE7951A4A6, 0x522061A1C2C2668B, 0xDC010181C3C3E77C, 0xDF010181C3C3E7FF, 0x20FEFE7E3C3C1800,
    }, blackPSQT = new ulong[96];


    public MyBot()
    {
        for (int i = 0; i < whitePSQT.Length; ++i)
        {
            var bytes = BitConverter.GetBytes(whitePSQT[i]);
            Array.Reverse(bytes);
            blackPSQT[i] = BitConverter.ToUInt64(bytes);
        }
    }

    MoveValue NegaMax(int depth, int alpha, int beta)
    {
        Move bestMove = Move.NullMove;

        int currentEval = 0;

        ulong key = board.ZobristKey;

        TTEntry entry = tt[key % entries];

        bool notRoot = depth != rootDepth;

        // TT cutoffs
        if (notRoot &&  entry.key == key && entry.depth >= depth && (
            entry.bound == 3 // exact score
                || entry.bound == 2 && entry.moveValue.value >= beta // lower bound, fail high
                || entry.bound == 1 && entry.moveValue.value <= alpha // upper bound, fail low
        ))
        {
            return entry.moveValue;
        }
        // quiescence
        if (depth <= 0) 
        {
            currentEval = EvaluateColour(board.IsWhiteToMove) - EvaluateColour(!board.IsWhiteToMove);
            if (currentEval >= beta) return new (bestMove, beta);
            if (currentEval > alpha) alpha = currentEval;
        }

        var moves = board.GetLegalMoves(depth <= 0);

        // sorting
        var moveScores = new int[moves.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            if (moves[i] == entry.moveValue.move) moveScores[i] = -100000;
            if (moves[i].IsCapture) moveScores[i] = pieceValues[(int)moves[i].MovePieceType - 1] / 10 - pieceValues[(int)moves[i].CapturePieceType - 1];
        }
        Array.Sort(moveScores, moves);

        if (depth != rootDepth && board.IsRepeatedPosition()) return new(bestMove, 0);


        if (moves.Length == 0)
        {
            if (depth <= 0) return new (bestMove, currentEval);
            if (board.IsInCheck()) return new(bestMove, CHECKMATE_EVAL - depth);
            return new(bestMove, 0);
        }

        // bound set to upper
        int bound = 1;

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int newEval = -NegaMax(depth - 1, -beta, -alpha).value;
            board.UndoMove(move);

            if (newEval >= beta)
            {
                bound = 2; // lower
                alpha = beta;
                bestMove = move;
                break;
            }
            
            if (newEval > alpha)
            {
                bound = 3; // exact
                bestMove = move;
                alpha = newEval;
            }
        }

        tt[key % entries] = new(key, new(bestMove, alpha), depth, bound);

        return new (bestMove, alpha);
    }

    int EvaluateColour(bool isWhite)
    {
        ulong GetPieceBitboard(PieceType pieceType) => board.GetPieceBitboard(pieceType, isWhite);

        var psqt = isWhite? whitePSQT: blackPSQT;
        int res = 0, gamePhase = 0, eg = 0, mg = 0;
        for (int piece = 0; piece < 6; piece++)
        {
            PieceType pieceType = (PieceType)piece + 1;

            int count = board.GetPieceList(pieceType, isWhite).Count;
            res += count * pieceValues[piece];
            gamePhase += count * gamePhaseIncs[piece];
            ulong posBitBoard = GetPieceBitboard(pieceType);

            for (int b = 0; b < 8; b++) 
            {
                int getPSQT(int x) => PopCount(posBitBoard & psqt[piece * 8 + b + x]) * (1 << b);
                mg += getPSQT(0);
                eg += getPSQT(48);
            }
        }
        //gamePhase = Math.Max(gamePhase, 24);
        // KING SAFETY
        ulong kingBB = GetPieceBitboard(PieceType.King), enemyBB = isWhite ? board.BlackPiecesBitboard : board.WhitePiecesBitboard; ;
        int kingPos = BitboardHelper.ClearAndGetIndexOfLSB(ref kingBB);
        ulong kingQuadrantBitboard = BitboardHelper.GetKingAttacks(new (kingPos));
        // bad if there are more enemies in the king's quadrant than friendies
        mg -= attackWeights[PopCount(enemyBB & kingQuadrantBitboard)];
        
        mg -= PopCount(BitboardHelper.GetSliderAttacks(PieceType.Queen, new (kingPos), board)) / 2;
        // BISHOP PAIR
        if (board.GetPieceList(PieceType.Bishop, isWhite).Count >= 2) res += 30;
        
        res += (mg * gamePhase + eg * (24 - gamePhase)) / 24;
        
        return res;
    }
   

    int PopCount(ulong bitboard) => BitOperations.PopCount(bitboard);

}

