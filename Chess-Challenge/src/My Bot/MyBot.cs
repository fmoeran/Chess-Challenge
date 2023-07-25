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

    int[] gamePhaseIncs = { 0, 1, 1, 2, 4, 0 };

    static Board board;
    Timer timer;

    int rootDepth = 0;
    
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
        int rootDepth = 0;
        while (!ShouldFinishSearch())
        {
            rootDepth++;
            bestMoveValueThisIteration = NegaMax(rootDepth, NEGATIVE_INFINITY, POSITIVE_INFINITY);
            bestMoveValue = bestMoveValueThisIteration;
        }
        Console.WriteLine("Depth: " + rootDepth);
        Console.WriteLine("Eval: " + bestMoveValue.value);
        return bestMoveValue.move;
    }

    ulong[] whitePSQT = {
         0xFF84E945B57084FF, 0x005ACA33883F3400, 0x002C0F45B5075000, 0xFF1ADE9D2230BEFF, 0xFFEA030426FE42FF, 0xFF9CB385A7BF9FFF, 0xFFA38385A7BF9FFF, 0x007F7C7A58406000,
         0x328E81AEB4F0CA7D, 0x687A716DA856EA46, 0xD8E5775E36B06935, 0xC2D6C8A852C1D0D7, 0x2A4AB51983021E00, 0xF6E45A218383DDFB, 0x4CC321018383DFFE, 0x103CFEFE7C7C2000,
         0xB092210E01314C47, 0xFCD118453650908E, 0x5FF76B7F896E1746, 0x6E14A5CA8821B9CC, 0xEE88FEFB9101D972, 0xD72D81C3810199BF, 0xFB8D81C3810199FF, 0x00727E3C7EFE6600,
         0x52B5DBC241FE6719, 0x5A1C2AF403128CE5, 0x5A49D70C634EC2F8, 0xC8E1249DC93D1765, 0x8F9229666D715966, 0x103C41C7EEFE5EA7, 0x000001C7EFFFDFE7, 0xFFFFFE3810002018,
         0x23566A2FC0D556E0, 0x8C28DC80EF63DA74, 0x15561E2FA29170B2, 0xE65B5EDC50AB9813, 0xE20E4DD0FDBEDA85, 0x13BDAFDFFFBFDB77, 0x031F0FDFFFBFDBF7, 0xFCE0F02000402408,
         0x1414F8FB6CB7ACB2, 0xCB7517F201639FDF, 0x0CE928E98DC3A231, 0x4B47FEC33A5FBB51, 0x507A2584BB18971A, 0x68BEBD7F46E7A731, 0x79FEBDFFFFFFBF39, 0x86014200000040C6,
         0xFF143BC864B426FF, 0x001FA406658A5700, 0x0002E5E81C278600, 0xFF2D293AC05B66FF, 0xFF6BE539FCFFE6FF, 0xFF57DD38FCFFE6FF, 0xFF7F0238FCFFE6FF, 0x007FFFC703001900,
         0xF9A7C27F3EE2ABD0, 0x53B7E24208622788, 0x2B2500C1F72A086B, 0x4348D3DFC9C5F3BC, 0x869AB04242269D42, 0xBC7F73C3C3E77E3D, 0x7FFFF3C3C3E7FFFF, 0x00000C3C3C180000,
         0x4D9166514B2681E8, 0x2BD703D5DEB66112, 0x51D6A00159C6D7E8, 0xC384FDC012A45BA5, 0x3C7FFFC1D3673C5A, 0xFFFFFFC1D3E7FFFF, 0xFFFFFFC1D3E7FFFF, 0x0000003E2C180000,
         0xC0F9E802F7C6C794, 0x75F6A7BE4541B887, 0xCC50FF013E958350, 0xC0E01FFB0F8A0CCE, 0xC0F0FFFBFF7FFF7F, 0xC0F0FFFBFFFFFFFF, 0xC0F0FFFBFFFFFFFF, 0x3F0F000400000000,
         0xBEFB53835EAB5DD7, 0xA17A71FDBC00C4E8, 0xB84B4A2057AF5812, 0x1EE11B0F9B897BCC, 0x018C3AA968884098, 0x01910351018BBF77, 0x01810301018BFFFF, 0xFE7EFCFEFE740000,
         0x919E52AB9027D339, 0x0144B4852AD318A5, 0xEC3A1A581362E530, 0xCC4064DE3D59A4E4, 0x502160A182C26689, 0xDE01008183C3E77E, 0xDF01008183C3E7FF, 0x20FEFF7E7C3C1800
    };

    ulong[] blackPSQT ;


    public MyBot()
    {
        blackPSQT = new ulong[whitePSQT.Length];
        for (int i = 0; i < whitePSQT.Length; ++i)
        {
            byte[] bytes = BitConverter.GetBytes(whitePSQT[i]);
            Array.Reverse(bytes);
            blackPSQT[i] = BitConverter.ToUInt64(bytes);
        }

       
    }


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


        if (depth != rootDepth &&  board.IsRepeatedPosition()) return new(bestMove, 0);


        if (moves.Length == 0)
        {
            if (depth <= 0) return new (bestMove, currentEval);
            if (board.IsInCheck()) return new(bestMove, CHECKMATE_EVAL - depth);
            return new(bestMove, 0);
        }

        SortMoves(ref moves);

        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int newEval = -NegaMax(depth - 1, -beta, -alpha).value;
            board.UndoMove(move);

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
        return EvaluateColour(board.IsWhiteToMove) - EvaluateColour(!board.IsWhiteToMove);
    }
    
    int EvaluateColour(bool isWhite)
    {
        var psqt = isWhite? whitePSQT: blackPSQT;
        int res = 0, gamePhase = 0, eg = 0, mg = 0;
        for (int piece = 0; piece < 6; piece++)
        {
            PieceType pieceType = (PieceType)piece + 1;

            int count = board.GetPieceList(pieceType, isWhite).Count;
            // PIECE SQUARE VALUE SUMS
            res += count * pieceValues[piece];
            gamePhase += count * gamePhaseIncs[piece];

            // PIECE SQUARE ESTIMATES
            ulong posBitBoard = board.GetPieceBitboard(pieceType, true);

            for (int b = 0; b < 8; b++)
            {
                mg += PopCount(posBitBoard & psqt[piece * 8 + b]) * (1 << b);
                eg += PopCount(posBitBoard & psqt[piece * 8 + b + whitePSQT.Length/2]) * (1 << b);
            }
        }

        gamePhase = Math.Max(gamePhase, 24);
        ulong enemyPawnBB = board.GetPieceBitboard(PieceType.Pawn, !isWhite);
        ulong enemyBB = isWhite ? board.BlackPiecesBitboard : board.WhitePiecesBitboard;
        int enemyCount = PopCount(enemyBB);
        // KING SAFETY
        ulong kingBB = board.GetPieceBitboard(PieceType.King, isWhite);
        int kingPos = BitboardHelper.ClearAndGetIndexOfLSB(ref kingBB);
        ulong kingQuadrantBitboard = getQuadrantBitboard(kingPos);
        
        int attackingPiecesCount = PopCount(enemyBB ^ enemyPawnBB ^ board.GetPieceBitboard(PieceType.King, !isWhite) & kingQuadrantBitboard);
        // bad if there are more enemies in the king's quadrant than friendies
        mg -= attackWeights[attackingPiecesCount] / 5;
        
        mg -= PopCount(BitboardHelper.GetSliderAttacks(PieceType.Queen, new (kingPos), board)) ;
        // BISHOP PAIR
        if (PopCount(board.GetPieceBitboard(PieceType.Bishop, isWhite)) >= 2) res += 30;

        // PAWN STRUCTURE
        //foreach (int index in IterBitboard(friendlyPawnBB))
        //{

        //}
        // ROOK OPEN FILES

        res += (mg * gamePhase + eg * (24 - gamePhase)) / 24;
        
        return res;
    }
   

    void SortMoves(ref Move[] moves)
    {

        var moveScores = new int[moves.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];
            if (move.IsCapture)
            {   
                moveScores[i] += pieceValues[(int)move.CapturePieceType-1] - pieceValues[(int)move.MovePieceType-1] / 10;

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
        return timer.MillisecondsElapsedThisTurn > 50;
    }
    
    
}