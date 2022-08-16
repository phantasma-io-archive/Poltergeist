using Poltergeist.PhantasmaLegacy.Core.Performance;
using Poltergeist.PhantasmaLegacy.Core.Types;
using Poltergeist.PhantasmaLegacy.Cryptography;
using Poltergeist.PhantasmaLegacy.Domain;
using Poltergeist.PhantasmaLegacy.Numerics;
using Poltergeist.PhantasmaLegacy.Storage;
using Poltergeist.PhantasmaLegacy.Storage.Context;
using Poltergeist.PhantasmaLegacy.VM;
using System.Collections.Generic;

namespace Poltergeist.PhantasmaLegacy.Blockchain.Contracts
{
    public struct GasLoanEntry
    {
        public Hash hash;
        public Address borrower;
        public Address lender;
        public BigInteger amount;
        public BigInteger interest;
    }

    public struct GasLender
    {
        public BigInteger balance;
        public Address paymentAddress;
    }

    public sealed class GasContract : NativeContract
    {
        public override NativeContractKind Kind => NativeContractKind.Gas;

        internal StorageMap _allowanceMap; //<Address, BigInteger>
        internal StorageMap _allowanceTargets; //<Address, Address>

        internal BigInteger _rewardAccum;

        internal Timestamp _lastInflationDate;
        internal bool _inflationReady;

        public void AllowGas(Address from, Address target, BigInteger price, BigInteger limit)
        {
            if (Runtime.IsReadOnlyMode())
            {
                return;
            }

            if (_lastInflationDate == 0)
            {
                _lastInflationDate = Runtime.Time;
            }

            Runtime.Expect(from.IsUser, "must be a user address");
            Runtime.Expect(Runtime.PreviousContext.Name == VirtualMachine.EntryContextName, "must be entry context");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(target.IsSystem, "destination must be system address");

            Runtime.Expect(price > 0, "price must be positive amount");
            Runtime.Expect(limit > 0, "limit must be positive amount");

            if (target.IsNull)
            {
                target = Runtime.Chain.Address;
            }

            var maxAmount = price * limit;

            using (var m = new ProfileMarker("_allowanceMap"))
            {
                var allowance = _allowanceMap.ContainsKey(from) ? _allowanceMap.Get<Address, BigInteger>(from) : 0;
                Runtime.Expect(allowance == 0, "unexpected pending allowance");

                allowance += maxAmount;
                _allowanceMap.Set(from, allowance);
                _allowanceTargets.Set(from, target);
            }

            BigInteger balance;
            using (var m = new ProfileMarker("Runtime.GetBalance"))
                balance = Runtime.GetBalance(DomainSettings.FuelTokenSymbol, from);
            Runtime.Expect(balance >= maxAmount, $"not enough {DomainSettings.FuelTokenSymbol} {balance} in address {from} {maxAmount}");

            using (var m = new ProfileMarker("Runtime.TransferTokens"))
                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, this.Address, maxAmount);
            using (var m = new ProfileMarker("Runtime.Notify"))
                Runtime.Notify(EventKind.GasEscrow, from, new GasEventData(target, price, limit));
        }

        public void SpendGas(Address from)
        {
            if (Runtime.IsReadOnlyMode())
            {
                return;
            }

            Runtime.Expect(Runtime.PreviousContext.Name == VirtualMachine.EntryContextName, "must be entry context");
            Runtime.Expect(Runtime.IsWitness(from), "invalid witness");
            Runtime.Expect(_allowanceMap.ContainsKey(from), "no gas allowance found");

            if (Runtime.ProtocolVersion >= 3)
            {
                SpendGasV2(from);
            }
            else
            {
                SpendGasV1(from);
            }

        }

        private void SpendGasV1(Address from)
        {
            var availableAmount = _allowanceMap.Get<Address, BigInteger>(from);

            var spentGas = Runtime.UsedGas;
            var requiredAmount = spentGas * Runtime.GasPrice;
            Runtime.Expect(requiredAmount > 0, "gas fee must exist");

            Runtime.Expect(availableAmount >= requiredAmount, "gas allowance is not enough");

            var targetAddress = _allowanceTargets.Get<Address, Address>(from);
            BigInteger targetGas;

            Runtime.Notify(EventKind.GasPayment, from, new GasEventData(targetAddress,  Runtime.GasPrice, spentGas));

            // return escrowed gas to transaction creator
            Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, from, availableAmount);

            Runtime.Expect(spentGas > 1, "gas spent too low");
            var burnGas = spentGas / 2;

            if (burnGas > 0)
            {
                Runtime.BurnTokens(DomainSettings.FuelTokenSymbol, from, burnGas);
                spentGas -= burnGas;
            }

            targetGas = spentGas / 2; // 50% for dapps (or reward accum if dapp not specified)

            if (targetGas > 0)
            {
                var targetPayment = targetGas * Runtime.GasPrice;

                if (targetAddress == Runtime.Chain.Address)
                {
                    _rewardAccum += targetPayment;
                    Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, this.Address, targetPayment);
                    Runtime.Notify(EventKind.CrownRewards, from, new TokenEventData(DomainSettings.FuelTokenSymbol, targetPayment, Runtime.Chain.Name));
                }
                else
                {
                    Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, targetAddress, targetPayment);
                }
                spentGas -= targetGas;
            }

            if (spentGas > 0)
            {
                var validatorPayment = spentGas * Runtime.GasPrice;
                var validatorAddress = SmartContract.GetAddressForNative(NativeContractKind.Block);
                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, from, validatorAddress, validatorPayment);
                spentGas = 0;
            }

            _allowanceMap.Remove(from);
            _allowanceTargets.Remove(from);

            CheckInflation();
        }

        private void SpendGasV2(Address from)
        {
            var availableAmount = _allowanceMap.Get<Address, BigInteger>(from);

            var spentGas = Runtime.UsedGas;
            var requiredAmount = spentGas * Runtime.GasPrice;
            Runtime.Expect(requiredAmount > 0, $"{Runtime.GasPrice} {spentGas} gas fee must exist");

            Runtime.Expect(availableAmount >= requiredAmount, "gas allowance is not enough");

            var leftoverAmount = availableAmount - requiredAmount;

            var targetAddress = _allowanceTargets.Get<Address, Address>(from);
            BigInteger targetGas;

            Runtime.Notify(EventKind.GasPayment, from, new GasEventData(targetAddress,  Runtime.GasPrice, spentGas));

            // return leftover escrowed gas to transaction creator
            if (leftoverAmount > 0)
            {
                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, from, leftoverAmount);
            }

            Runtime.Expect(spentGas > 1, "gas spent too low");
            var burnGas = spentGas / 2;

            if (burnGas > 0)
            {
                BigInteger burnAmount;
                
                if (Runtime.ProtocolVersion >= 4)
                {
                    burnAmount = burnGas * Runtime.GasPrice;
                }
                else
                {
                    burnAmount = burnGas;
                }

                Runtime.BurnTokens(DomainSettings.FuelTokenSymbol, this.Address, burnAmount);
                spentGas -= burnGas;
            }

            targetGas = spentGas / 2; // 50% for dapps (or reward accum if dapp not specified)

            if (targetGas > 0)
            {
                var targetPayment = targetGas * Runtime.GasPrice;

                if (targetAddress == Runtime.Chain.Address)
                {
                    _rewardAccum += targetPayment;
                    Runtime.Notify(EventKind.CrownRewards, from, new TokenEventData(DomainSettings.FuelTokenSymbol, targetPayment, Runtime.Chain.Name));
                }
                else
                {
                    Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, targetAddress, targetPayment);
                }
                spentGas -= targetGas;
            }

            if (spentGas > 0)
            {
                var validatorPayment = spentGas * Runtime.GasPrice;
                var validatorAddress = SmartContract.GetAddressForNative(NativeContractKind.Block);
                Runtime.TransferTokens(DomainSettings.FuelTokenSymbol, this.Address, validatorAddress, validatorPayment);
                spentGas = 0;
            }

            _allowanceMap.Remove(from);
            _allowanceTargets.Remove(from);

            CheckInflation();
        }

        private void CheckInflation()
        {
            if (Runtime.HasGenesis && Runtime.TransactionIndex == 0)
            {
                if (_lastInflationDate.Value == 0)
                {
                    var genesisTime = Runtime.GetGenesisTime();
                    _lastInflationDate = genesisTime;
                }
                else
                if (!_inflationReady)
                {
                    var infDiff = Runtime.Time - _lastInflationDate;
                    var inflationPeriod = SecondsInDay * 90;
                    if (infDiff >= inflationPeriod)
                    {
                        _inflationReady = true;
                    }
                }
            }
        }
    }
}
