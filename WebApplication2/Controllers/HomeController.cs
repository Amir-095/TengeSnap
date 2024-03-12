using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using WebApplication2.Models;
using System.Net.Http;
using System.Xml;
using System.Threading.Tasks;
using System.Globalization;
using System.Collections.Generic;

namespace WebApplication2.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        // Метод для получения данных о курсе валюты
        [HttpGet]
        public async Task<float> GetExchangeRate(string date, string currencyCode)
        {
            var httpClient = new HttpClient();
            var url = $"https://nationalbank.kz/rss/get_rates.cfm?fdate={date}";
            var response = await httpClient.GetStringAsync(url);
            var xmlDocument = new XmlDocument();
            xmlDocument.LoadXml(response);
            var xmlNodeList = xmlDocument.GetElementsByTagName("item");
            foreach (XmlNode node in xmlNodeList)
            {
                if (node["title"].InnerText == currencyCode)  // Используйте переданный код валюты
                {
                    return float.Parse(node["description"].InnerText, CultureInfo.InvariantCulture);
                }
            }
            return 0;
        }


        private async Task<List<float>> GetExchangeRatesForPeriod(string currencyCode, int days)
        {
            // Запускаем все задачи параллельно
            var tasks = Enumerable.Range(0, days).Select(i => GetExchangeRate(DateTime.Now.AddDays(-i).ToString("dd.MM.yyyy"), currencyCode));
            var rates = await Task.WhenAll(tasks);

            // Возвращаем результаты в виде списка
            return rates.ToList();
        }


        // Метод для получения данных о курсе валюты за последнюю неделю
        private async Task<List<float>> GetExchangeRatesForWeek(string currencyCode)
        {
            var rates = new List<float>();
            for (int i = 0; i < 7; i++)
            {
                var date = DateTime.Now.AddDays(-i).ToString("dd.MM.yyyy");
                var rate = await GetExchangeRate(date, currencyCode);  // Передайте код валюты в метод GetExchangeRate
                rates.Add(rate);
            }
            return rates;
        }

        public async Task<IActionResult> Currency(string id, string specificDate = null, int period = 7)
        {
            if (specificDate != null)
            {
                // Если указана конкретная дата, получите курс валюты только за эту дату
                var rate = await GetExchangeRate(specificDate, id);
                ViewBag.Dates = new List<string> { specificDate };
                ViewBag.Rates = new List<float> { rate };
            }
            else
            {
                // Если дата не указана, получите курсы валюты за период
                var dates = new List<string>();
                for (int i = 0; i < period; i++)
                {
                    var date = DateTime.Now.AddDays(-i).ToString("dd.MM.yyyy");
                    dates.Add(date);
                }
                var rates = await GetExchangeRatesForPeriod(id, period);
                dates.Reverse();
                rates.Reverse();
                ViewBag.Dates = dates;
                ViewBag.Rates = rates;
            }

            ViewBag.Currency = id;
            return View();
        }


        public async Task<IActionResult> Index()
        {
            var dates = new List<string>();
            for (int i = 0; i < 7; i++)
            {
                var date = DateTime.Now.AddDays(-i).ToString("dd.MM.yyyy");
                dates.Add(date);
            }
            dates.Reverse();
            ViewBag.Dates = dates;

            var currencies = new List<string> { "RUB", "EUR", "USD", "BYN", "CAD", "CNY", "JPY", "KGS", "AUD" };
            var rates = new Dictionary<string, List<float>>();
            var weekChanges = new Dictionary<string, float>();

            // Запускаем все задачи параллельно
            var tasks = currencies.Select(currency => GetExchangeRatesForWeek(currency));
            var results = await Task.WhenAll(tasks);

            // Заполняем словари rates и weekChanges результатами задач
            for (int i = 0; i < currencies.Count; i++)
            {
                var currency = currencies[i];
                var currencyRates = results[i];
                rates[currency] = currencyRates;
                weekChanges[currency] = currencyRates.First() - currencyRates.Last();
            }

            ViewBag.Rates = rates;
            ViewBag.WeekChanges = weekChanges;

            return View();
        }

        public async Task<IActionResult> Crypto(string specificDate = null)
        {
            string id = "USD"; // Устанавливаем код валюты на USD
            if (specificDate != null)
            {
                // Если указана конкретная дата, получите курс валюты только за эту дату
                var rate = await GetExchangeRate(specificDate, id);
                ViewBag.Dates = new List<string> { specificDate };
                ViewBag.Rates = new List<float> { rate };
            }
            else
            {
                // Если дата не указана, получите курсы валюты за последние 7 дней
                var dates = new List<string>();
                for (int i = 0; i < 7; i++)
                {
                    var date = DateTime.Now.AddDays(-i).ToString("dd.MM.yyyy");
                    dates.Add(date);
                }
                var rates = await GetExchangeRatesForPeriod(id, 7);
                dates.Reverse();
                rates.Reverse();
                ViewBag.Dates = dates;
                ViewBag.Rates = rates;
            }

            ViewBag.Currency = id;
            return View();
        }

        [HttpGet]
        public IActionResult Converter()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Converter(float amount, string fromCurrency, string toCurrency)
        {
            var date = DateTime.Now.ToString("dd.MM.yyyy");
            float rateFrom;
            if (fromCurrency == "KZT")
            {
                rateFrom = 1;
            }
            else
            {
                rateFrom = await GetExchangeRate(date, fromCurrency);
            }

            float convertedAmount;
            if (toCurrency == "KZT")
            {
                // Конвертируем из исходной валюты в тенге
                convertedAmount = amount * rateFrom;
            }
            else
            {
                // Конвертируем из тенге в целевую валюту
                var rateTo = await GetExchangeRate(date, toCurrency);
                convertedAmount = (amount / rateTo) * rateFrom;
            }

            ViewBag.ConvertedAmount = convertedAmount;
            return View();
        }



        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
    