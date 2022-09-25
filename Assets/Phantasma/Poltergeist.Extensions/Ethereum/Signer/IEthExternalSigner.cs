using System.Threading.Tasks;

namespace Poltergeist.PhantasmaLegacy.Ethereum.Signer
{
#if !DOTNET35

    public enum ExternalSignerTransactionFormat
    {
        RLP,
        Hash,
        Transaction
    }
    
#endif
}