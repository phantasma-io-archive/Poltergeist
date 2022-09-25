using System.Collections.Generic;

namespace Poltergeist.PhantasmaLegacy.Ethereum.RLP
{
    public class RLPCollection : List<IRLPElement>, IRLPElement
    {
        public byte[] RLPData { get; set; }
    }
}
