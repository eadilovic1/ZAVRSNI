using ePinPong.Interfaces;
using ePinPong.Models;

namespace ePinPong.Services.BracketStrategies
{
    /// <summary>
    /// Strategija za Double Elimination sa Utješnim turnirom (razigravanje poraženih + posebni utješni bracket za 3+ plasirane u grupi).
    /// </summary>
    public class DoubleEliminationUtjesniBracketStrategy : BaseBracketDrawStrategy
    {
        public override SistemTurnira Sistem => SistemTurnira.DoubleEliminationUtjesni;
        protected override bool HasLoserBracket => true;
        protected override bool HasUtjesniBracket => true;

        public DoubleEliminationUtjesniBracketStrategy(IRandomProvider rng) : base(rng) { }
    }
}
