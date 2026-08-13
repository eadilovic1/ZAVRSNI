using ePinPong.Interfaces;
using ePinPong.Models;

namespace ePinPong.Services.BracketStrategies
{
    /// <summary>
    /// Strategija za Double Elimination sistem turnira (sa razigravanjem poraženih za plasman, bez utješnog turnira).
    /// </summary>
    public class DoubleEliminationBracketStrategy : BaseBracketDrawStrategy
    {
        public override SistemTurnira Sistem => SistemTurnira.DoubleElimination;
        protected override bool HasLoserBracket => true;
        protected override bool HasUtjesniBracket => false;

        public DoubleEliminationBracketStrategy(IRandomProvider rng) : base(rng) { }
    }
}
