using ePinPong.Interfaces;
using ePinPong.Models;

namespace ePinPong.Services.BracketStrategies
{
    /// <summary>
    /// Strategija za Single Elimination sistem turnira (bez utješnog dijela i bez razigravanja poraženih).
    /// </summary>
    public class SingleEliminationBracketStrategy : BaseBracketDrawStrategy
    {
        public override SistemTurnira Sistem => SistemTurnira.SingleElimination;
        protected override bool HasLoserBracket => false;
        protected override bool HasUtjesniBracket => false;

        public SingleEliminationBracketStrategy(IRandomProvider rng) : base(rng) { }
    }
}
