using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using PMCommonEntities.Models;
using PMUnifiedAPI.Models;

/*
 * PMFundManagerConsole - Pseudo Fund Management Console for Pseudo Markets
 * Author: Shravan Jambukesan <shravan@shravanj.com>
 * Date: 9/4/2020
 */

namespace PMFundManagerConsole
{
    
    public class Program
    {
        public static IConfigurationRoot configuration;
        public static string BASE_URL = "https://app.pseudomarkets.live";
        public static void Main(string[] args)
        {
            ServiceCollection serviceCollection = new ServiceCollection();

            // Configure the config service so we can fetch the connection string from it
            ConfigureServices(serviceCollection);
            ClientMenu();
        }

        private static void ConfigureServices(IServiceCollection serviceCollection)
        {
            // Inject Serilog
            serviceCollection.AddSingleton(LoggerFactory.Create(builder =>
            {
                builder.AddSerilog(dispose: true);
            }));

            serviceCollection.AddLogging();

            // Setup our application config
            configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("appsettings.json", false)
                .Build();

            serviceCollection.AddSingleton<IConfigurationRoot>(configuration);

            serviceCollection.AddTransient<Program>();
        }

        public static void ClientMenu()
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("PSEUDO MARKETS FUND MANAGER CONSOLE");
            Console.WriteLine("===========================================");
            Console.WriteLine("1. VIEW PSEUDO FUNDS");
            Console.WriteLine("2. CREATE PSEUDO FUND");
            Console.WriteLine("3. UPDATE FUND SECURITIES");
            Console.WriteLine("4. VIEW FUND SECURITIES");
            Console.WriteLine("5. VIEW FUND HISTORY");
            Console.WriteLine("6. VIEW FUND TRADES");
            Console.WriteLine("7. UPDATE FUND NAV");
            Console.WriteLine("8. EXIT");
            Console.Write("Enter selection: ");
            string input = Console.ReadLine();
            switch (input)
            {
                case "1":
                    ViewPseudoFunds();
                    break;
                case "2":
                    CreatePseudoFund();
                    break;
                case "3":
                    UpdateFundSecurities();
                    break;
                case "4":
                    ViewFundSecurities();
                    break;
                case "5":
                    ViewFundHistory();
                    break;
                case "6":
                    ViewFundTrades();
                    break;
                case "7":
                    UpdateFundNav();
                    break;
                case "8":
                    break;
                default:
                    Console.WriteLine("Please enter a valid selection (1 - 8)");
                    ClientMenu();
                    break;
            }
        }

        public static void UpdateFundSecurities()
        {
            try
            {
                using (var db = new PseudoMarketsDbContext())
                {
                    Console.WriteLine("===========================================");
                    Console.WriteLine("UPDATE FUND UNDERLYING SECURITIES" + "\n");
                    Console.Write("ENTER FUND TICKER: ");
                    string fundTicker = Console.ReadLine();

                    var existingFund = db.PseudoFunds.FirstOrDefault(x => x.FundTicker == fundTicker);
                    var fundId = existingFund.Id;

                    if (existingFund != null && fundId > 0)
                    {
                        Console.Write("ENTER EQUITY TICKER TO ADD: ");
                        string equityTicker = Console.ReadLine();
                        Console.Write("ENTER QUANTITY: ");
                        int quantity = Int32.Parse(Console.ReadLine());
                        Console.Write("ENTER ORDER TYPE (BUY/SELL): ");
                        string orderType = Console.ReadLine();
                        var client = new HttpClient();
                        var response = client.GetAsync(BASE_URL + "/api/Quotes/SmartQuote/" + equityTicker);
                        var responseString = response.Result.Content.ReadAsStringAsync();
                        var responseJson = JsonConvert.DeserializeObject<LatestPriceOutput>(responseString.Result);
                        double orderTotal = responseJson.price * quantity;
                        Console.WriteLine("ORDER SUMMARY: " + orderType + " " + quantity + " SHARES OF " + responseJson.symbol + " @ " + responseJson.price + " = $" + orderTotal);
                        Console.Write("EXECUTE (y/n)?: ");
                        string action = Console.ReadLine().ToUpper();
                        if (action == "Y")
                        {
                            if (orderType.ToUpper() == "BUY")
                            {
                                PseudoFundUnderlyingSecurities newSecurity = new PseudoFundUnderlyingSecurities()
                                {
                                    FundId = fundId,
                                    PurchasePrice = orderTotal,
                                    Quantity = quantity,
                                    Ticker = equityTicker
                                };

                                db.PseudoFundUnderlyingSecurities.Add(newSecurity);

                                existingFund.InitialInvestment += orderTotal;

                                db.Entry(existingFund).State = EntityState.Modified;

                                db.SaveChanges();
                            }
                            else
                            {
                                var existingSecurity =
                                    db.PseudoFundUnderlyingSecurities.FirstOrDefault(x => x.Ticker == equityTicker);
                                if (existingSecurity != null)
                                {
                                    double costBasis = existingSecurity.PurchasePrice * existingSecurity.Quantity;
                                    double currentValue = orderTotal;

                                    double gainOrLoss = currentValue - costBasis;

                                    existingSecurity.Quantity -= quantity;
                                    existingFund.InitialInvestment += gainOrLoss;

                                    db.Entry(existingFund).State = EntityState.Modified;
                                    db.Entry(existingSecurity).State = EntityState.Modified;

                                    db.SaveChanges();
                                }
                                else
                                {
                                    Console.WriteLine("SECURITY DOES NOT CURRENTLY EXIST IN THIS FUND");
                                    ClientMenu();
                                }
                            }

                            Console.WriteLine("SUCCESSFULLY UPDATED SECURITIES FOR FUND " + fundTicker);
                            Log.Information("Executed: " + orderType + " " + quantity + " SHARES OF " + responseJson.symbol + " @ " + responseJson.price + " = $" + orderTotal);

                        }
                        else
                        {
                            Console.WriteLine("ORDER CANCELLED");
                            ClientMenu();
                        }

                    }
                    else
                    {
                        Console.WriteLine("INVALID FUND TICKER");
                        ClientMenu();
                    }

                    Console.WriteLine("FUND " + fundTicker + " CREATED SUCCESSFULLY");
                    Log.Information("Created Pseudo Fund with ticker: " + fundTicker + " on " + DateTime.Now);
                }
                Console.WriteLine("===========================================");
                Console.WriteLine("Enter to return back to menu...");
                Console.ReadKey();
                ClientMenu();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static void CreatePseudoFund()
        {
            try
            {
                using (var db = new PseudoMarketsDbContext())
                {
                    Console.WriteLine("===========================================");
                    Console.WriteLine("CREATE PSEUDO FUND" + "\n");
                    Console.Write("ENTER FUND NAME: ");
                    string fundName = Console.ReadLine();
                    Console.Write("ENTER FUND TICKER: ");
                    string fundTicker = Console.ReadLine();
                    Console.Write("ENTER FUND DESC: ");
                    string fundDesc = Console.ReadLine();
                    Console.Write("ENTER INITIAL SHARES OUTSTANDING: ");
                    int sharesOutstanding = Int32.Parse(Console.ReadLine()); 

                    PseudoFunds newFund = new PseudoFunds()
                    {
                        FundName = fundName,
                        FundDescription = fundDesc,
                        FundTicker = fundTicker,
                        InitialInvestment = 0,
                        SharesOutstanding = sharesOutstanding
                    };

                    db.PseudoFunds.Add(newFund);
                    db.SaveChanges();

                    Console.WriteLine("FUND " + fundTicker + " CREATED SUCCESSFULLY");
                    Log.Information("Created Pseudo Fund with ticker: " + fundTicker + " on " + DateTime.Now);
                }
                Console.WriteLine("===========================================");
                Console.WriteLine("Enter to return back to menu...");
                Console.ReadKey();
                ClientMenu();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static void UpdateFundNav()
        {
            try
            {
                using (var db = new PseudoMarketsDbContext())
                {
                    Console.WriteLine("===========================================");
                    Console.WriteLine("MANUALLY UPDATE FUND NAV" + "\n");
                    Console.Write("ENTER FUND TICKER: ");
                    string fundTicker = Console.ReadLine();

                    var existingFund = db.PseudoFunds.FirstOrDefault(x => x.FundTicker == fundTicker);
                    int fundId = existingFund.Id;
                    double currentInvestmentValue = 0;

                    if (fundId > 0 && fundId != null)
                    {
                        var securities = db.PseudoFundUnderlyingSecurities.Where(x => x.FundId == fundId).ToList();
                        foreach (PseudoFundUnderlyingSecurities security in securities)
                        {
                            currentInvestmentValue += GetCurrentSecurityPrice(security.Ticker) * security.Quantity;
                        }
                    }

                    double nav = currentInvestmentValue / existingFund.SharesOutstanding;

                    PseudoFundHistories newHistory = new PseudoFundHistories()
                    {
                        FundId = fundId,
                        CurrentNav = nav,
                        ClosingDate = DateTime.Today
                    };

                    db.PseudoFundHistories.Add(newHistory);
                    db.SaveChanges();

                    Console.WriteLine("NAV SUCCESSFULLY UPDATED");
                    Log.Information("NAV for Fund: " + fundTicker + " updated on " + DateTime.Now);

                }
                Console.WriteLine("===========================================");
                Console.WriteLine("Enter to return back to menu...");
                Console.ReadKey();
                ClientMenu();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static double GetCurrentSecurityPrice(string symbol)
        {
            var client = new HttpClient();
            var response = client.GetAsync(BASE_URL + "/api/Quotes/SmartQuote/" + symbol);
            var responseString = response.Result.Content.ReadAsStringAsync();
            var responseJson = JsonConvert.DeserializeObject<LatestPriceOutput>(responseString.Result);

            return responseJson.price;
        }

        public static void ViewFundSecurities()
        {
            try
            {
                using (var db = new PseudoMarketsDbContext())
                {
                    Console.WriteLine("===========================================");
                    Console.WriteLine("VIEW FUND UNDERLYING SECURITIES" + "\n");
                    Console.Write("ENTER FUND TICKER: ");
                    string ticker = Console.ReadLine();

                    int fundId = db.PseudoFunds.FirstOrDefault(x => x.FundTicker == ticker).Id;

                    if (fundId > 0)
                    {
                        var securities = db.PseudoFundUnderlyingSecurities.Where(x => x.FundId == fundId).ToList();
                        foreach (PseudoFundUnderlyingSecurities security in securities)
                        {
                            Console.WriteLine("===========================================");
                            Console.WriteLine("EQUITY TICKER: " + security.Ticker);
                            Console.WriteLine("QUANTITY: " + security.Quantity);
                            Console.WriteLine("PURCHASE PRICE: $" + security.PurchasePrice);
                            Console.WriteLine("===========================================");
                        }
                    }
                    else
                    {
                        Console.WriteLine("INVALID FUND TICKER");
                    }

                }
                Console.WriteLine("===========================================");
                Console.WriteLine("Enter to return back to menu...");
                Console.ReadKey();
                ClientMenu();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static void ViewFundHistory()
        {
            try
            {
                using (var db = new PseudoMarketsDbContext())
                {
                    Console.WriteLine("===========================================");
                    Console.WriteLine("VIEW FUND HISTORY" + "\n");
                    Console.Write("ENTER FUND TICKER: ");
                    string ticker = Console.ReadLine();

                    int fundId = db.PseudoFunds.FirstOrDefault(x => x.FundTicker == ticker).Id;

                    if (fundId > 0)
                    {
                        var fundHistory = db.PseudoFundHistories.Where(x => x.FundId == fundId).ToList();
                        foreach (PseudoFundHistories history in fundHistory)
                        {
                            Console.WriteLine("NAV: $" + history.CurrentNav);
                            Console.WriteLine("DATE: " + history.ClosingDate);
                        }
                    }
                    else
                    {
                        Console.WriteLine("INVALID FUND TICKER");
                    }

                }
                Console.WriteLine("===========================================");
                Console.WriteLine("Enter to return back to menu...");
                Console.ReadKey();
                ClientMenu();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static void ViewFundTrades()
        {
            try
            {
                using (var db = new PseudoMarketsDbContext())
                {
                    Console.WriteLine("===========================================");
                    Console.WriteLine("VIEW FUND TRADES" + "\n");
                    Console.Write("ENTER FUND TICKER: ");
                    string ticker = Console.ReadLine();

                    var orders = db.Orders.Where(x => x.Symbol == ticker).ToList();

                    if (orders != null && orders.Count > 0)
                    {
                        foreach (Orders order in orders)
                        {
                            Console.WriteLine("TYPE: " + order.Type);
                            Console.WriteLine("PRICE: $" + order.Price);
                            Console.WriteLine("QUANTITY: " + order.Quantity);
                            Console.WriteLine("DATE: " + order.Date);
                            Console.WriteLine("TRANSACTION ID: " + order.TransactionID);
                        }
                    }
                    else
                    {
                        Console.WriteLine("INVALID FUND TICKER");
                    }
                }
                Console.WriteLine("===========================================");
                Console.WriteLine("Enter to return back to menu...");
                Console.ReadKey();
                ClientMenu();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static void ViewPseudoFunds()
        {
            try
            {
                using (var db = new PseudoMarketsDbContext())
                {
                    Console.WriteLine("===========================================");
                    Console.WriteLine("PSEUDO FUNDS" + "\n");
                    var pseudoFunds = db.PseudoFunds.ToList();
                    foreach (PseudoFunds fund in pseudoFunds)
                    {
                        Console.WriteLine("FUND ID: " + fund.Id);
                        Console.WriteLine("FUND NAME: " + fund.FundName);
                        Console.WriteLine("FUND TICKER: " + fund.FundTicker);
                        Console.WriteLine("FUND DESC: " + fund.FundDescription);
                        Console.WriteLine("FUND INIT INVESTMENT: $" + fund.InitialInvestment);
                        Console.WriteLine("SHARES OUTSTANDING: " + fund.SharesOutstanding);
                    }
                }
                Console.WriteLine("===========================================");
                Console.WriteLine("Enter to return back to menu...");
                Console.ReadKey();
                ClientMenu();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }

    public class LatestPriceOutput
    {
        public string symbol { get; set; }
        public double price { get; set; }
        public DateTime timestamp { get; set; }
        public string source { get; set; }
    }

}
