/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using NUnit.Framework;
using QuantConnect.Brokerages.Samco;

namespace QuantConnect.Tests.Brokerages.Samco
{
    [TestFixture]
    public class SamcoSymbolMapperTests
    {
        private readonly SamcoSymbolMapper _symbolMapper = new();

        [TestCase("tcs", "BSE")]
        [TestCase("reliance", "BSE")]
        [TestCase("bse", "NSE")]
        [TestCase("cdsl", "NSE")]
        public void GetExchangeWithSymbol(string ticker, string expectedExchange)
        {
            var symbol = Symbol.Create(ticker, SecurityType.Equity, Market.India);
            var actualExchange = _symbolMapper.GetExchange(symbol);
            Assert.AreEqual(expectedExchange, actualExchange);
        }

        [TestCase("532540_BSE", "BSE")]
        [TestCase("19585_NSE", "NSE")]
        public void GetExchangeWithListingID(string listingID, string expectedExchange)
        {
            var actualExchange = _symbolMapper.GetExchange(listingID);
            Assert.AreEqual(expectedExchange, actualExchange);
        }

        [TestCase("tcs")]
        [TestCase("ltts")]
        public void IsKnownBrokerageSymbol(string symbol)
        {
            Assert.IsTrue(_symbolMapper.IsKnownBrokerageSymbol(symbol));
        }

        [TestCase("tcs")]
        [TestCase("infy")]
        [TestCase("bse")]
        [TestCase("cdsl")]
        public void GetSymbolFromAllSegment(string symbol)
        {
            Assert.IsNotNull(_symbolMapper.GetSymbolFromAllSegment(symbol));
        }
    }
}