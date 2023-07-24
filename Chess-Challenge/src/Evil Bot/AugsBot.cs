//#define DEBUG_TIMER
using ChessChallenge.API;
using System;
using System.Numerics;

public class AugsBot : IChessBot
{
	UInt64[] kWhitePTables = { 0xFF8470B545E984FF, 0x00343F8833CA5A00, 0x005007B5450F2C00, 0xFFBE30229DDE1AFF, 0xFF42FE260403EAFF, 0xFF9FBFA785B39CFF, 0xFF9FBFA78583A3FF, 0x006040587A7C7F00,
						 0x7DCAF0B4AE818E32, 0x46EA56A86D717A68, 0x3569B0365E77E5D8, 0xD7D0C152A8C8D6C2, 0x001E028319B54A2A, 0xFBDD8383215AE4F6, 0xFEDF83830121C34C, 0x00207C7CFEFE3C10,
						 0x474C31010E2192B0, 0x8E9050364518D1FC, 0x46176E897F6BF75F, 0xCCB92188CAA5146E, 0x72D90191FBFE88EE, 0xBF990181C3812DD7, 0xFF990181C3818DFB, 0x0066FE7E3C7E7200,
						 0x1967FE41C2DBB552, 0xE58C1203F42A1C5A, 0xF8C24E630CD7495A, 0x65173DC99D24E1C8, 0x6659716D6629928F, 0xA75EFEEEC7413C10, 0xE7DFFFEFC7010000, 0x1820001038FEFFFF,
						 0xE056D5C02F6A5623, 0x74DA63EF80DC288C, 0xB27091A22F1E5615, 0x1398AB50DC5E5BE6, 0x85DABEFDD04D0EE2, 0x77DBBFFFDFAFBD13, 0xF7DBBFFFDF0F1F03, 0x0824400020F0E0FC,
						 0xB2ACB76CFBF81414, 0xDF9F6301F21775CB, 0x31A2C38DE928E90C, 0x51BB5F3AC3FE474B, 0x1A9718BB84257A50, 0x31A7E7467FBDBE68, 0x39BFFFFFFFBDFE79, 0xC640000000420186,
						 0xFF26B464C83B14FF, 0x00578A6506A41F00, 0x0086271CE8E50200, 0xFF665BC03A292DFF, 0xFFE6FFFC39E56BFF, 0xFFE6FFFC38DD57FF, 0xFFE6FFFC38027FFF, 0x00190003C7FF7F00,
						 0xD0ABE23E7FC2A7F9, 0x8827620842E2B753, 0x6B082AF7C100252B, 0xBCF3C5C9DFD34843, 0x429D264242B09A86, 0x3D7EE7C3C3737FBC, 0xFFFFE7C3C3F3FF7F, 0x0000183C3C0C0000,
						 0xE881264B5166914D, 0x1261B6DED503D72B, 0xE8D7C65901A0D651, 0xA55BA412C0FD84C3, 0x5A3C67D3C1FF7F3C, 0xFFFFE7D3C1FFFFFF, 0xFFFFE7D3C1FFFFFF, 0x0000182C3E000000,
						 0x94C7C6F702E8F9C0, 0x87B84145BEA7F675, 0x5083953E01FF50CC, 0xCE0C8A0FFB1FE0C0, 0x7FFF7FFFFBFFF0C0, 0xFFFFFFFFFBFFF0C0, 0xFFFFFFFFFBFFF0C0, 0x0000000004000F3F,
						 0xD75DAB5E8353FBBE, 0xE8C400BCFD717AA1, 0x1258AF57204A4BB8, 0xCC7B899B0F1BE11E, 0x98408868A93A8C01, 0x77BF8B0151039101, 0xFFFF8B0101038101, 0x000074FEFEFC7EFE,
						 0x39D32790AB529E91, 0xA518D32A85B44401, 0x30E56213581A3AEC, 0xE4A4593DDE6440CC, 0x8966C282A1602150, 0x7EE7C383810001DE, 0xFFE7C383810001DF, 0x00183C7C7EFFFE20 };
	UInt64[] kBlackPTables;

	//                     .  P    K    B    R    Q    K
	int[] kPieceValues = { 0, 100, 300, 310, 500, 900, 10000 };
	int kMassiveNum = 99999999;

#if DEBUG_TIMER
	int dNumMovesMade = 0;
	int dTotalMsElapsed = 0;
#endif

	int mDepth;
	Move mBestMove;

	public AugsBot()
	{
		kBlackPTables = new UInt64[kWhitePTables.Length];
		for (int i = 0; i < kWhitePTables.Length; ++i)
			kBlackPTables[i] = ReverseBits(kWhitePTables[i]);
	}

	public Move Think(Board board, Timer timer)
	{
		Move[] legalMoves = board.GetLegalMoves();
		mDepth = 6;

		EvaluateBoardNegaMax(board, mDepth, -kMassiveNum, kMassiveNum, board.IsWhiteToMove ? 1 : -1);

#if DEBUG_TIMER
		dNumMovesMade++;
		dTotalMsElapsed += timer.MillisecondsElapsedThisTurn;
		Console.WriteLine("My bot time average: {0}", (float)dTotalMsElapsed / dNumMovesMade);
#endif
		return mBestMove;
	}

	int EvaluateBoardNegaMax(Board board, int depth, int alpha, int beta, int color)
	{
		Move[] legalMoves;

		if (board.IsDraw())
			return 0;

		if (depth == 0 || (legalMoves = board.GetLegalMoves()).Length == 0)
		{
			// EVALUATE

			if (board.IsInCheckmate())
				return -depth - 9999999;

			return color * (EvalColor(board, true) - EvalColor(board, false));
			// EVALUATE
		}

		// TREE SEARCH
		SortMoves(ref legalMoves);
		int recordEval = int.MinValue;
		foreach (Move move in legalMoves)
		{
			board.MakeMove(move);
			int evaluation = -EvaluateBoardNegaMax(board, depth - 1, -beta, -alpha, -color);
			board.UndoMove(move);

			if (recordEval < evaluation)
			{
				recordEval = evaluation;
				if (depth == mDepth)
					mBestMove = move;
			}
			alpha = Math.Max(alpha, recordEval);
			if (alpha >= beta) break;
		}
		// TREE SEARCH

		return recordEval;
	}

	void SortMoves(ref Move[] moves)
	{
		Move temp;
		for (int i = 1, j = 0; i < moves.Length; ++i)
		{
			if (moves[i].IsCapture || moves[i].IsPromotion)
			{
				temp = moves[j];
				moves[j++] = moves[i];
				moves[i] = temp;
			}
		}
	}

	int EvalColor(Board board, bool isWhite)
	{
		UInt64[] PTable = isWhite ? kWhitePTables : kBlackPTables;
		int sum = 0;
		for (int i = 1; i < 7; ++i)
		{
			ulong pieceBitBoard = board.GetPieceBitboard((PieceType)i, isWhite);
			sum += kPieceValues[i] * BitOperations.PopCount(pieceBitBoard);
			for (int b = 0; b < 8; ++b)
			{
				sum += BitOperations.PopCount(pieceBitBoard & PTable[(i - 1) * 8 + b]) * (1 << b);
			}
		}
		return sum;
	}

	public static UInt64 ReverseBits(UInt64 num)
	{
		num = ((num & 0x5555555555555555) << 1) | ((num >> 1) & 0x5555555555555555);
		num = ((num & 0x3333333333333333) << 2) | ((num >> 2) & 0x3333333333333333);
		num = ((num & 0x0F0F0F0F0F0F0F0F) << 4) | ((num >> 4) & 0x0F0F0F0F0F0F0F0F);
		num = ((num & 0x00FF00FF00FF00FF) << 8) | ((num >> 8) & 0x00FF00FF00FF00FF);
		num = ((num & 0x0000FFFF0000FFFF) << 16) | ((num >> 16) & 0x0000FFFF0000FFFF);
		num = (num << 32) | (num >> 32);

		return num;
	}
}