using Raylib_cs;
using System.Numerics;
using System;

namespace ChessChallenge.Application
{
    public static class MatchStatsUI
    {
        public static void DrawMatchStats(ChallengeController controller)
        {
            if (controller.PlayerWhite.IsBot && controller.PlayerBlack.IsBot)
            {
                int nameFontSize = UIHelper.ScaleInt(40);
                int regularFontSize = UIHelper.ScaleInt(35);
                int headerFontSize = UIHelper.ScaleInt(45);
                Color col = new(180, 180, 180, 255);
                Vector2 startPos = UIHelper.Scale(new Vector2(1500, 250));
                float spacingY = UIHelper.Scale(35);

                DrawNextText($"Game {controller.CurrGameNumber} of {controller.TotalGameCount}", headerFontSize, Color.WHITE);
                startPos.Y += spacingY * 2;

                DrawStats(controller.BotStatsA);
                startPos.Y += spacingY * 2;
                DrawStats(controller.BotStatsB);
                startPos.Y += spacingY * 2;
                // draw ELO diff
                double score = (controller.BotStatsA.NumWins + controller.BotStatsA.NumDraws * 0.5);
                double total = (double)controller.CurrGameNumber-1;
                double perc = score / total;
                double eloDiff = -400 * Math.Log(1 / perc - 1) / 2.303;

                if(controller.CurrGameNumber == 1)DrawNextText($"Elo Difference: Nan", headerFontSize, Color.WHITE);
                else if((int) eloDiff == -2147483648){
                    if(eloDiff > 0)DrawNextText($"Elo Difference: +Inf", headerFontSize, Color.WHITE);
                    else DrawNextText($"Elo Difference: -Inf", headerFontSize, Color.WHITE);
                }
                else if(eloDiff > 0)DrawNextText($"Elo Difference: +{(int)eloDiff}", headerFontSize, Color.WHITE);
                else DrawNextText($"Elo Difference: {(int)eloDiff}", headerFontSize, Color.WHITE);
           

                void DrawStats(ChallengeController.BotMatchStats stats)
                {
                    DrawNextText(stats.BotName + ":", nameFontSize, Color.WHITE);
                    DrawNextText($"Score: +{stats.NumWins} ={stats.NumDraws} -{stats.NumLosses}", regularFontSize, col);
                    DrawNextText($"Num Timeouts: {stats.NumTimeouts}", regularFontSize, col);
                    DrawNextText($"Num Illegal Moves: {stats.NumIllegalMoves}", regularFontSize, col);
                }
           
                void DrawNextText(string text, int fontSize, Color col)
                {
                    UIHelper.DrawText(text, startPos, fontSize, 1, col);
                    startPos.Y += spacingY;
                }
            }
        }
    }
}