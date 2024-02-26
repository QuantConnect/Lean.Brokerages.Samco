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
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Logging;
using QuantConnect.Securities;
using QuantConnect.Tests.Engine.DataFeeds;
using System;

namespace QuantConnect.Tests.Brokerages.Samco
{
    [TestFixture, Ignore("This test requires a configured and active Samco account")]
    public class SamcoBrokerageHistoryProviderTests
    {
        private static TestCaseData[] TestParameters
        {
            get
            {
                return new[]
                {
                    // valid parameters
                    new TestCaseData(Symbols.SBIN, Resolution.Minute, TickType.Trade, Time.OneHour, false),

                    // invalid resolution
                    new TestCaseData(Symbols.SBIN, Resolution.Tick, TickType.Trade, Time.OneMinute, true),
                    new TestCaseData(Symbols.SBIN, Resolution.Second, TickType.Trade, Time.OneMinute, true),
                    new TestCaseData(Symbols.SBIN, Resolution.Hour, TickType.Trade, Time.OneDay, true),
                    new TestCaseData(Symbols.SBIN, Resolution.Daily, TickType.Trade, TimeSpan.FromDays(15), true),

                    // invalid tick type
                    new TestCaseData(Symbols.SBIN, Resolution.Minute, TickType.Quote, Time.OneHour, true),
                    new TestCaseData(Symbols.SBIN, Resolution.Minute, TickType.OpenInterest, Time.OneHour, true),

                    // invalid security type
                    new TestCaseData(Symbols.USDJPY, Resolution.Minute, TickType.Trade, Time.OneHour, true),

                    // invalid market
                    new TestCaseData(Symbol.Create("SBIN", SecurityType.Equity, Market.USA), Resolution.Minute, TickType.Trade, Time.OneHour, true),

                    // invalid time range
                    new TestCaseData(Symbols.SBIN, Resolution.Minute, TickType.Trade, TimeSpan.FromDays(-15), true),
                };
            }
        }

        [Test, TestCaseSource(nameof(TestParameters))]
        public void GetsHistory(Symbol symbol, Resolution resolution, TickType tickType, TimeSpan period, bool unsupported)
        {
            var apiSecret = Config.Get("samco-client-password");
            var apiKey = Config.Get("samco-client-id");
            var yob = Config.Get("samco-year-of-birth");
            var tradingSegment = Config.Get("samco-trading-segment");
            var productType = Config.Get("samco-product-type");

            var now = DateTime.UtcNow;

            var algorithm = new AlgorithmStub();
            algorithm.Portfolio = new SecurityPortfolioManager(new SecurityManager(new TimeKeeper(now)), algorithm.Transactions, algorithm.Settings);
            var security = algorithm.AddSecurity(symbol);
            algorithm.Portfolio.Securities.Add(security);

            var brokerage = new SamcoBrokerage(tradingSegment, productType, apiKey, apiSecret, yob, algorithm, null);

            var request = new HistoryRequest(now.Add(-period),
                now,
                typeof(TradeBar),
                symbol,
                resolution,
                SecurityExchangeHours.AlwaysOpen(TimeZones.Kolkata),
                TimeZones.Kolkata,
                Resolution.Minute,
                false,
                false,
                DataNormalizationMode.Adjusted,
                tickType);

            var history = brokerage.GetHistory(request);

            if (unsupported)
            {
                Assert.IsNull(history);
                return;
            }

            Assert.IsNotNull(history);

            foreach (var slice in history)
            {
                Log.Trace("{0}: {1} - {2} / {3}", slice.Time, slice.Symbol, slice.Price, slice.IsFillForward);
            }

            Log.Trace("Base currency: " + brokerage.AccountBaseCurrency);
        }
    }
}