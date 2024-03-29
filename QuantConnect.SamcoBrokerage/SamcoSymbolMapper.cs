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

using CsvHelper;
using CsvHelper.Configuration;
using QuantConnect.Brokerages.Samco.SamcoMessages;
using QuantConnect.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace QuantConnect.Brokerages.Samco
{
    /// <summary>
    /// Provides the mapping between Lean symbols and Samco symbols.
    /// </summary>
    public class SamcoSymbolMapper : ISymbolMapper
    {
        /// <summary>
        /// The list of known Samco symbols.
        /// </summary>
        private readonly List<ScripMaster> _samcoTradableSymbolList = new();
        private Dictionary<string, string> _listingidExchangeMapping = new();
        private readonly string _getSymbolsEndpoint = "https://developers.stocknote.com/doc/ScripMaster.csv";

        /// <summary>
        /// List of Samco Symbols that are Tradable
        /// </summary>
        /// <returns>A List of Samco Symbols</returns>
        public List<ScripMaster> SamcoSymbols => _samcoTradableSymbolList;

        /// <summary>
        /// Constructs default instance of the Samco Sybol Mapper
        /// </summary>
        public SamcoSymbolMapper()
        {
            var csvString = _getSymbolsEndpoint.DownloadData();
            TextReader sr = new StringReader(csvString);
            CsvConfiguration configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            };
            var csv = new CsvReader(sr, configuration);
            var scrips = csv.GetRecords<ScripMaster>();
            foreach (var scrip in scrips)
            {
                _samcoTradableSymbolList.Add(scrip);
                _listingidExchangeMapping[scrip.SymbolCode] = scrip.Exchange;
            }
        }

        /// <summary>
        /// Converts an Samco symbol to a Lean symbol instance
        /// </summary>
        /// <param name="scrip">A Lean symbol instance</param>
        /// <returns>A new Lean Symbol instance</returns>
        public static Symbol CreateLeanSymbol(ScripMaster scrip)
        {
            if (scrip == null)
            {
                throw new ArgumentNullException(nameof(scrip));
            }
            char[] sep = { '-' };

            var securityType = SecurityType.Equity;
            var market = Market.India;
            OptionRight optionRight = 0;
            switch (scrip.Instrument)
            {
                //Equities
                case "EQ":
                    securityType = SecurityType.Equity;
                    break;
                //Index Options
                case "OPTIDX":
                    securityType = SecurityType.Option;
                    break;
                //Stock Futures
                case "FUTSTK":
                    securityType = SecurityType.Future;
                    break;
                //Stock options
                case "OPTSTK":
                    securityType = SecurityType.Option;
                    break;
                //Commodity Futures
                case "FUTCOM":
                    securityType = SecurityType.Future;
                    break;
                //Commodity Options
                case "OPTCOM":
                    securityType = SecurityType.Option;
                    break;
                //Bullion Options
                case "OPTBLN":
                    securityType = SecurityType.Option;
                    break;
                //Energy Futures
                case "FUTENR":
                    securityType = SecurityType.Future;
                    break;
                //Currenty Options
                case "OPTCUR":
                    securityType = SecurityType.Option;
                    break;
                //Currency Futures
                case "FUTCUR":
                    securityType = SecurityType.Option;
                    break;
                //Bond Futures
                case "FUTIRC":
                    securityType = SecurityType.Future;
                    break;
                //Bond Futures
                case "FUTIRT":
                    securityType = SecurityType.Future;
                    break;
                //Bond Option
                case "OPTIRC":
                    securityType = SecurityType.Option;
                    break;

                default:
                    securityType = SecurityType.Base;
                    break;
            }

            string[] ticker = scrip.TradingSymbol.Split(sep);

            Symbol symbol = null;

            if (securityType == SecurityType.Option)
            {
                if (ticker[0].EndsWithInvariant("PE", true))
                {
                    optionRight = OptionRight.Put;
                }
                if (ticker[0].EndsWithInvariant("CE", true))
                {
                    optionRight = OptionRight.Call;
                }

                var strikePrice = Convert.ToDecimal(scrip.StrikePrice, CultureInfo.InvariantCulture);
                var expiryDate = DateTime.ParseExact(scrip.ExpiryDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                symbol = Symbol.CreateOption(ConvertSamcoSymbolToLeanSymbol(scrip.Name.Replace(" ", "").Trim()), market, OptionStyle.European, optionRight, strikePrice, expiryDate);
            }

            if (securityType == SecurityType.Future)
            {
                var expiryDate = DateTime.ParseExact(scrip.ExpiryDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                symbol = Symbol.CreateFuture(ConvertSamcoSymbolToLeanSymbol(ticker[0].Trim().Replace(" ", "")), market, expiryDate);
            }
            if (securityType == SecurityType.Equity)
            {
                symbol = Symbol.Create(ConvertSamcoSymbolToLeanSymbol(scrip.Name.Trim().Replace(" ", "")), securityType, market);
            }

            return symbol;
        }

        /// <summary>
        /// Fetch all samco symbols for a given LEAN symbol
        /// </summary>
        /// <param name="leanSymbol">LEAN symbol</param>
        /// <returns>A list of Samco ScripMaster</returns>
        public List<ScripMaster> GetSamcoTokenList(Symbol leanSymbol)
        {
            List<ScripMaster> tokenList = new();
            if (leanSymbol.SecurityType == SecurityType.Equity)
            {
                var nseScrip = GetSymbolFromNseEquitySegment(leanSymbol.ID.Symbol); ;
                if (nseScrip != null)
                {
                    tokenList.Add(nseScrip);
                } 
            }
            var scrip = GetSymbolFromAllSegmentExceptNseEquity(leanSymbol.ID.Symbol);
            if (scrip != null)
            {
                tokenList.Add(scrip);
            }
            if (tokenList.IsNullOrEmpty())
            {
                throw new Exception($"SamcoSymbolMapper.GetSamcoTokenList(): symbol not found for given ticker {leanSymbol.ID.Symbol}");
            }
            return tokenList;
        }

        /// <summary>
        /// Returns the security type for an Samco symbol
        /// </summary>
        /// <param name="brokerageSymbol">The Samco symbol</param>
        /// <returns>The security type</returns>
        public SecurityType GetBrokerageSecurityType(string brokerageSymbol)
        {
            if (brokerageSymbol.Contains('-'))
            {
                brokerageSymbol = brokerageSymbol.Split('-')[0];
            }
            if (string.IsNullOrWhiteSpace(brokerageSymbol))
                throw new ArgumentException($"SamcoSymbolMapper.GetBrokerageSecurityType(): Invalid Samco symbol {brokerageSymbol}");

            var scrip = GetSymbolFromAllSegment(brokerageSymbol);
            if (scrip == null)
            {
                throw new ArgumentException($"SamcoSymbolMapper.GetBrokerageSecurityType(): Invalid Samco symbol {brokerageSymbol}");
            }
            var symbol = CreateLeanSymbol(scrip);
            return symbol.SecurityType;
        }

        /// <summary>
        /// Converts a Lean symbol instance to an Samco symbol
        /// </summary>
        /// <param name="symbol">A Lean symbol instance</param>
        /// <returns>The Samco symbol</returns>
        public string GetBrokerageSymbol(Symbol symbol)
        {
            if (symbol == null || string.IsNullOrWhiteSpace(symbol.Value))
                throw new ArgumentException("SamcoSymbolMapper.GetBrokerageSymbol(): Invalid symbol " + (symbol == null ? "null" : symbol.ToString()));

            var brokerageSymbol = ConvertLeanSymbolToSamcoSymbol(symbol.Value);

            return brokerageSymbol;
        }

        /// <summary>
        /// Fetches the trading segment inside Market.India, E.g: NSE, BSE for the given token
        /// </summary>
        /// <param name="symbol">LEAN symbol</param>
        /// <returns>An exchange value for the given token</returns>
        public string GetExchange(Symbol symbol)
        {
            if (symbol == null)
            {
                throw new ArgumentNullException(nameof(symbol));
            }
            var brokerageSymbol = ConvertLeanSymbolToSamcoSymbol(symbol.Value);
            var scrip = GetSymbolFromAllSegment(brokerageSymbol);
            if (scrip == null)
            {
                throw new ArgumentException($"SamcoSymbolMapper.GetExchange(): Invalid Samco symbol {brokerageSymbol}");
            }
            return scrip.Exchange;
        }

        /// <summary>
        /// Fetches the trading segment inside Market.India, E.g: NSE, BSE for the given token
        /// </summary>
        /// <param name="listingid">The Samco Instrument Token</param>
        /// <returns>An exchange value for the given token</returns>
        public string GetExchange(string listingid)
        {
            if (listingid.IsNullOrEmpty())
            {
                throw new ArgumentNullException(listingid);
            }
            if (!_listingidExchangeMapping.TryGetValue(listingid, out string exchange))
            {
                throw new Exception($"SamcoSymbolMapper.GetExchange(): scrip not found for given listingID {listingid}");
            }
            return exchange;
        }

        /// <summary>
        /// Converts an Samco symbol to a Lean symbol instance
        /// </summary>
        /// <param name="brokerageSymbol">The Samco symbol</param>
        /// <param name="securityType">The security type</param>
        /// <param name="market">The market</param>
        /// <param name="expirationDate">Expiration date of the security(if applicable)</param>
        /// <param name="strike">The strike of the security (if applicable)</param>
        /// <param name="optionRight">The option right of the security (if applicable)</param>
        /// <returns>A new Lean Symbol instance</returns>
        public Symbol GetLeanSymbol(string brokerageSymbol, SecurityType securityType, string market, DateTime expirationDate = default(DateTime), decimal strike = 0, OptionRight optionRight = OptionRight.Call)
        {
            if (brokerageSymbol.Contains('-'))
            {
                brokerageSymbol = brokerageSymbol.Split('-')[0];
            }

            if (string.IsNullOrWhiteSpace(brokerageSymbol))
                throw new ArgumentException($"SamcoSymbolMapper.GetLeanSymbol(): Invalid Samco symbol {brokerageSymbol}");

            if (securityType == SecurityType.Forex || securityType == SecurityType.Cfd || securityType == SecurityType.Commodity || securityType == SecurityType.Crypto)
                throw new ArgumentException($"SamcoSymbolMapper.GetLeanSymbol(): Unsupported security type {securityType}");

            if (!Market.Encode(market.ToLowerInvariant()).HasValue)
                throw new ArgumentException($"SamcoSymbolMapper.GetLeanSymbol(): Invalid market {market}");
            var scrip = GetSymbolFromAllSegment(brokerageSymbol);
            if (scrip == null)
            {
                throw new ArgumentException($"SamcoSymbolMapper.GetLeanSymbol(): Invalid Samco symbol {brokerageSymbol}");
            }
            return CreateLeanSymbol(scrip);
        }

        /// <summary>
        /// Converts an Samco symbol to a Lean symbol instance
        /// </summary>
        /// <param name="brokerageSymbol">The Samco symbol</param>
        /// <param name="securityType">The security type</param>
        /// <param name="expirationDate">Expiration date of the security(if applicable)</param>
        /// <param name="strike">The strike of the security (if applicable)</param>
        /// <param name="optionRight">The option right of the security (if applicable)</param>
        /// <returns>A new Lean Symbol instance</returns>
        public Symbol GetLeanSymbol(string brokerageSymbol, SecurityType securityType, DateTime expirationDate = default(DateTime), decimal strike = 0, OptionRight optionRight = OptionRight.Call)
        {
            var market = Market.India;
            return GetLeanSymbol(brokerageSymbol, securityType, market, expirationDate, strike, optionRight);
        }

        /// <summary>
        /// Checks if the symbol is supported by Samco
        /// </summary>
        /// <param name="brokerageSymbol">The Samco symbol</param>
        /// <returns>True if Samco supports the symbol otherwise false</returns>
        public bool IsKnownBrokerageSymbol(string brokerageSymbol)
        {
            var scrip = GetSymbolFromAllSegment(brokerageSymbol);
            if (scrip != null)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get symbol from All available segmens like NSE, BSE, CDS
        /// </summary>
        /// <param name="brokerageSymbol">The Samco symbol</param>
        /// <returns>An instance of <see cref="ScripMaster"/> class</returns>
        public ScripMaster GetSymbolFromAllSegment(string brokerageSymbol)
        {
            if (string.IsNullOrWhiteSpace(brokerageSymbol))
            {
                return null;
            }
            var scrip = GetSymbolFromAllSegmentExceptNseEquity(brokerageSymbol);
            if (scrip != null)
            {
                return scrip;
            }
            return GetSymbolFromNseEquitySegment(brokerageSymbol);
        }

        /// <summary>
        /// Gets trading symbol from NSE Equity Segment
        /// </summary>
        /// <returns>An instance of <see cref="ScripMaster"/> class</returns>
        private ScripMaster GetSymbolFromNseEquitySegment(string tradingSymbol)
        {
            // Modify for NSE Equities
            var nseTicker = tradingSymbol.ToUpperInvariant() + "-EQ";
            return _samcoTradableSymbolList.Where(x => x.TradingSymbol == nseTicker).SingleOrDefault();
        }

        /// <summary>
        /// Gets trading symbol from All Segments except NSE Equity Segment
        /// </summary>
        /// <returns>An instance of <see cref="ScripMaster"/> class</returns>
        private ScripMaster GetSymbolFromAllSegmentExceptNseEquity(string tradingSymbol)
        {
            return _samcoTradableSymbolList.Where(s => s.TradingSymbol == tradingSymbol.ToUpperInvariant()).SingleOrDefault();
        }

        /// <summary>
        /// Converts a Lean symbol string to an Samco symbol
        /// </summary>
        private static string ConvertLeanSymbolToSamcoSymbol(string leanSymbol)
        {
            if (string.IsNullOrWhiteSpace(leanSymbol))
                throw new ArgumentException($"SamcoSymbolMapper.ConvertLeanSymbolToSamcoSymbol(): Invalid Lean symbol {leanSymbol}");

            // return as it is due to Samco has similar Symbol format
            return leanSymbol.ToUpperInvariant();
        }

        /// <summary>
        /// Converts an Samco symbol to a Lean symbol string
        /// </summary>
        private static string ConvertSamcoSymbolToLeanSymbol(string samcoSymbol)
        {
            if (string.IsNullOrWhiteSpace(samcoSymbol))
                throw new ArgumentException($"SamcoSymbolMapper.ConvertLeanSymbolToSamcoSymbol(): Invalid Samco symbol {samcoSymbol}");

            // return as it is due to Samco has similar Symbol format
            return samcoSymbol.ToUpperInvariant();
        }
    }
}