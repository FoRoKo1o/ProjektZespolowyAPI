﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjektAPI.Contracts;
using ProjektAPI.Dtos;
using ProjektAPI.Models;
using ProjektAPI.Repository;

namespace ProjektAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BudgetsController : ControllerBase
    {
        private readonly IBudgetRepository _repository;
        private readonly IMapper _mapper;
        private readonly ICategoryRepository _categoryRepository;

        public BudgetsController(IMapper mapper, IBudgetRepository budgetRepository, ICategoryRepository categoryRepository)
        {
            _mapper = mapper;
            _repository = budgetRepository;
           _categoryRepository = categoryRepository;
        }

        // GET: api/Budgets
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BudgetDto>>> GetBudgets()
        {
            var budgets = await _repository.GetAllAsync();
            var records = _mapper.Map<List<Budget>>(budgets);
            return Ok(records);
        }

        [HttpGet]
        [Route("GetBudgetForUser")]
        public async Task<ActionResult<decimal>> GetBudgetForUser(int userId)
        {
            var budget = await _repository.GetBudgetByUserId(userId);

            if (budget == null)
            {
                return NotFound(); 
            }

            return Ok(budget);
        }

        [HttpGet]
        [Route("GetTotalExpensesForUser")]
        public async Task<ActionResult<decimal>> GetTotalExpensesForUser(int userId)
        {
            var totalExpenses = await _repository.GetTotalExpensesForUser(userId);
            var sortedExpenses = totalExpenses.OrderByDescending(e => e.Date).ToList();
            var expenseDtos = _mapper.Map<List<ExpenseDto>>(sortedExpenses);
            var total = expenseDtos.Sum(e => e.Price);
            return Ok(new { Expenses = expenseDtos, TotalExpenses = total });
        }

        [HttpGet]
        [Route("GetExpenseSummaries")]

        public async Task<ActionResult<object>> GetExpenseSummaries(int userId)
        {
            var expenses = await _repository.GetTotalExpensesForUser(userId);

            var yearly = expenses.GroupBy(e => e.Date.Year)
                .Select(g => new
                {
                    Year = g.Key,
                    TotalExpenses = g.Sum(e => e.Price),
                    Expenses = g.ToList()
                })
                .ToList();

            var monthly = new List<object>();
            for (int month = 1; month <= 12; month++)
            {
                var monthExpenses = expenses.Where(e => e.Date.Month == month);
                var monthTotalExpenses = monthExpenses.Sum(e => e.Price);
                var weeks = monthExpenses.GroupBy(e => CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(e.Date, CalendarWeekRule.FirstDay, DayOfWeek.Monday))
                                         .Select(g => new
                                         {
                                             Week = g.Key,
                                             TotalExpenses = g.Sum(e => e.Price),
                                             Expenses = g.ToList()
                                         })
                                         .ToList();

                monthly.Add(new
                {
                    Year = DateTime.Now.Year,
                    Month = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month),
                    TotalExpenses = monthTotalExpenses,
                    Weeks = weeks
                });
            }

            // Calculate expenses for each day in the current month, including one more day
            var currentMonthStartDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var nextMonthStartDate = currentMonthStartDate.AddMonths(1);
            var currentMonthExpenses = expenses.Where(e => e.Date >= currentMonthStartDate && e.Date < nextMonthStartDate);

            // Create a list of all days in the current month
            var allDaysInCurrentMonth = Enumerable.Range(0, (nextMonthStartDate - currentMonthStartDate).Days)
                                                  .Select(offset => currentMonthStartDate.AddDays(offset))
                                                  .ToList();

            // Calculate expenses for each day in the current month, including one additional day for the next month
            var dailyCurrentMonth = allDaysInCurrentMonth.Select(currentDate =>
            {
                var dayExpenses = currentMonthExpenses.Where(e => e.Date.Date == currentDate.Date);
                var dayTotalExpenses = dayExpenses.Sum(e => e.Price);

                return new
                {
                    Date = currentDate,
                    DayOfWeek = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(currentDate.DayOfWeek),
                    TotalExpenses = dayTotalExpenses,
                    Expenses = dayExpenses.ToList()
                };
            }).ToList();

            // Sort the data by date in ascending order
            dailyCurrentMonth = dailyCurrentMonth.OrderBy(d => d.Date).ToList();

            var currentMonthTotalExpenses = currentMonthExpenses.Sum(e => e.Price);

            var daily = new List<object>();
            var startDate = DateTime.Today.AddDays(-6);

            for (int i = 0; i < 7; i++)
            {
                var currentDate = startDate.AddDays(i);
                var dayExpenses = expenses.Where(e => e.Date.Date == currentDate.Date);
                var dayTotalExpenses = dayExpenses.Sum(e => e.Price);

                daily.Add(new
                {
                    Date = currentDate,
                    DayOfWeek = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(currentDate.DayOfWeek),
                    TotalExpenses = dayTotalExpenses,
                    Expenses = dayExpenses.ToList()
                });
            }

            var last31DaysExpenses = expenses.Where(e => e.Date >= DateTime.Today.AddDays(-30) && e.Date <= DateTime.Today);
            var allDaysInRange = Enumerable.Range(0, 31)
                                   .Select(offset => DateTime.Today.AddDays(-offset))
                                   .ToList();

            var dailyLast31Days = allDaysInRange.Select(currentDate =>
            {
                var dayExpenses = expenses.Where(e => e.Date.Date == currentDate.Date);
                var dayTotalExpenses = dayExpenses.Sum(e => e.Price);

                return new
                {
                    Date = currentDate,
                    DayOfWeek = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(currentDate.DayOfWeek),
                    TotalExpenses = dayTotalExpenses,
                    Expenses = dayExpenses.ToList()
                };
            }).ToList();

            // Sort the data by date in ascending order
            dailyLast31Days = dailyLast31Days.OrderBy(d => d.Date).ToList();

            var last31DaysTotalExpenses = dailyLast31Days.Sum(e => e.TotalExpenses);

            return Ok(new
            {
                Yearly = yearly,
                Monthly = monthly,
                Daily = daily,
                Last31DaysTotalExpenses = last31DaysTotalExpenses,
                DailyLast31Days = dailyLast31Days,
                CurrentMonth = currentMonthTotalExpenses,
                DailyCurrentMonth = dailyCurrentMonth
            });
        }

        [HttpGet]
        [Route("GetNextWeekForecast")]
        public async Task<ActionResult<double>> GetNextWeekForecast(int userId)
        {
            try
            {
                var expenses = await _repository.GetTotalExpensesForUser(userId);

                var endDate = DateTime.Today.AddDays(-1); 
                var startDate = endDate.AddDays(-28); 

                var lastFourWeeksExpenses = expenses.Where(e => e.Date >= startDate && e.Date <= endDate).ToList();

                var weeklyExpenses = lastFourWeeksExpenses.GroupBy(e => CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(e.Date, CalendarWeekRule.FirstDay, DayOfWeek.Monday))
                                                         .Select(g => g.Sum(e => e.Price))
                                                         .ToList();

                var average = weeklyExpenses.Count > 0 ? weeklyExpenses.Average() : 0;

                var lowerBound = average - (average * 0.1);
                var upperBound = average + (average * 0.1);
                return Ok(new
                {
                    average, lowerBound, upperBound
                });
            }
            catch (Exception ex)
            {
                return Ok("Missing data");
            }         
        }

        [HttpGet]
        [Route("GetNextMonthForecast")]
        public async Task<ActionResult<double>> GetNextMonthForecast(int userId)
        {
            try
            {
                var expenses = await _repository.GetTotalExpensesForUser(userId);

                var endDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddDays(-1);
                var startDate = endDate.AddMonths(-2).AddDays(1);

                var threeMonthsExpenses = expenses.Where(e => e.Date >= startDate && e.Date <= endDate).ToList();

                var threeMonthsSum = threeMonthsExpenses.Sum(e => e.Price);

                var average = threeMonthsExpenses.Count > 0 ? threeMonthsSum / 3 : 0;

                var lowerBound = average - (average * 0.1);
                var upperBound = average + (average * 0.1);

                return Ok(new { average, lowerBound, upperBound });
            }
            catch (Exception ex)
            {
                return Ok("Missing data");
            }
        }

        [HttpGet]
        [Route("GetNextYearForecast")]
        public async Task<ActionResult<double>> GetNextYearForecast(int userId)
        {
            try
            {
                var expenses = await _repository.GetTotalExpensesForUser(userId);

                var endDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddDays(-1);
                var startDate = endDate.AddYears(-2).AddDays(1);

                var threeYearsExpenses = expenses.Where(e => e.Date >= startDate && e.Date <= endDate).ToList();

                var threeYearsSum = threeYearsExpenses.Sum(e => e.Price);

                var average = threeYearsExpenses.Count > 0 ? threeYearsSum / 3 : 0;

                var lowerBound = average - (average * 0.1);
                var upperBound = average + (average * 0.1);

                return Ok(new { average, lowerBound, upperBound });
            }
            catch (Exception ex)
            {
                // Obsługa wyjątku lub braku danych
                return Ok("Missing data");
            }
        }

        [HttpGet]
        [Route("GetExpensesByUserByDate")]
        public async Task<ActionResult<ExpensesByUserByDateResponseDto>> GetExpensesByUserByDate(int userId, DateTime startDate, DateTime endDate)
        {
            var expenses = await _repository.GetExpensesByUserAndDate(userId, startDate, endDate);
            var expenseDtos = _mapper.Map<List<ExpenseDto>>(expenses);
            var totalExpense = expenses.Sum(e => e.Price);

            var responseDto = new ExpensesByUserByDateResponseDto
            {
                Expenses = expenseDtos,
                TotalExpense = totalExpense
            };

            return Ok(responseDto);
        }

        [HttpGet]
        [Route("GetTotalSpendingForUserPerPeriod")]
        public async Task<ActionResult<object>> GetTotalSpendingForUserPerPeriod(int userId, int days)
        {
            var lastExpense = await _repository.GetLastExpenseForUserPerPeriod(userId, days);

            if (lastExpense == null)
            {
                return Ok(new { TotalSpending = 0f, Expenses = new List<ExpenseDto>() });
            }

            var startDate = lastExpense.Date.AddDays(-days);
            var expenses = await _repository.GetExpensesByUserAndDate(userId, startDate, lastExpense.Date);
            var totalSpending = expenses.Sum(e => (double)e.Price);

            var expenseDtos = _mapper.Map<List<ExpenseDto>>(expenses);

            return Ok(new { TotalSpending = totalSpending, Expenses = expenseDtos });
        }

        // GET: api/Budgets/5
        [HttpGet("{id}")]
        public async Task<ActionResult<BudgetDto>> GetBudget(int id)
        {
            var budget = await _repository.GetAsync(id);
            if (budget == null)
            {
                return NotFound();
            }
            return _mapper.Map<BudgetDto>(budget);
        }

        // PUT: api/Budgets/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutBudget(int userId, EditableBudgetDto editableBudgetDto)
        {
            var bu = await _repository.GetAsync(userId);
            if (bu == null)
            {
                return NotFound();
            }

            // Only update the allowed properties from the DTO
            bu.BudgetLimit = editableBudgetDto.BudgetLimit;

            try
            {
                await _repository.UpdateAsync(bu);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await BudgetExists(userId))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            // Return the edited budget response (with some fields disabled)
            var editedBudgetResponse = new EditableBudgetDto
            {
                UserId = bu.UserId,
                BudgetId = bu.BudgetId,
                BudgetLimit = bu.BudgetLimit,
                BudgetSpent = bu.BudgetSpent,
                BudgetRemaining = bu.BudgetRemaining
                // Other properties will be included in the response, but they will be read-only (disabled)
                // You don't need to set them here since they will be mapped automatically by the framework.
            };

            return Ok(editedBudgetResponse);
        }

        [HttpGet]
        [Route("GetMostSpentCategoryForUser")]
        public async Task<ActionResult<object>> GetCategoriesAndExpensesForUser(int userId)
        {
            var expenses = await _repository.GetTotalExpensesForUser(userId);

            if (expenses == null || !expenses.Any())
            {
                return Ok("No expenses for this user");
            }

            var categoryExpenses = expenses.GroupBy(e => e.CategoryId)
                                           .Select(g => new
                                           {
                                               CategoryId = g.Key,
                                               TotalExpenses = g.Sum(e => e.Price)
                                           })
                                           .ToList();

            if (!categoryExpenses.Any())
            {
                return Ok("No expenses found");
            }

            var mostSpentCategory = categoryExpenses.OrderByDescending(c => c.TotalExpenses)
                                                    .FirstOrDefault();

            var categories = await _categoryRepository.GetAllAsync(); // Assuming you have a method to get all categories from the repository

            var categoryExpensesWithDetails = categoryExpenses.Select(c =>
            {
                var category = categories.FirstOrDefault(cat => cat.CategoryId == c.CategoryId);
                return new
                {
                    Category = _mapper.Map<CategoryDto>(category),
                    TotalExpenses = c.TotalExpenses
                };
            }).ToList();

            var result = new
            {
                MostSpentCategory = categoryExpensesWithDetails.FirstOrDefault(c => c.Category.CategoryId == mostSpentCategory.CategoryId),
                AllCategoryExpenses = categoryExpensesWithDetails
            };

            return Ok(result);
        }
        public enum TimePeriod
        {
            Week,
            Month,
            Year
        }
        [HttpGet]
        [Route("GetExpenseStatisticsForPeriod")]
        public async Task<ActionResult<object>> GetExpenseStatisticsForPeriod(int userId, TimePeriod period)
        {
            // Pobierz wydatki dla użytkownika
            var expenses = await _repository.GetTotalExpensesForUser(userId);

            // Sprawdź, czy użytkownik ma jakiekolwiek wydatki
            if (expenses == null || !expenses.Any())
            {
                return Ok("No expenses for this user");
            }

            // Określ zakres dat dla okresu, w którym obliczane są statystyki
            DateTime startDate;
            DateTime endDate = DateTime.Now;

            switch (period)
            {
                case TimePeriod.Week:
                    startDate = endDate.AddDays(-7);
                    break;
                case TimePeriod.Month:
                    startDate = endDate.AddMonths(-1);
                    break;
                case TimePeriod.Year:
                    startDate = endDate.AddYears(-1);
                    break;
                default:
                    return BadRequest("Invalid period");
            }

            // Wybierz tylko wydatki z określonego okresu
            var expensesForPeriod = expenses.Where(e => e.Date >= startDate && e.Date <= endDate).ToList();

            // Sprawdź, czy istnieją wydatki w wybranym okresie
            if (!expensesForPeriod.Any())
            {
                return Ok($"No expenses found for the {period} period");
            }

            // Zgrupuj wydatki w okresie według kategorii i oblicz sumę wydatków w każdej kategorii
            var categoryExpenses = expensesForPeriod.GroupBy(e => e.CategoryId)
                                                    .Select(g => new
                                                    {
                                                        CategoryId = g.Key,
                                                        TotalExpenses = g.Sum(e => e.Price)
                                                    })
                                                    .ToList();

            // Znajdź kategorię z największymi wydatkami w okresie
            var mostSpentCategory = categoryExpenses.OrderByDescending(c => c.TotalExpenses)
                                                    .FirstOrDefault();

            // Znajdź kategorię, której wydatki występują najczęściej w okresie
            var mostFrequentCategory = categoryExpenses.OrderByDescending(c => c.TotalExpenses)
                                                      .GroupBy(c => c.TotalExpenses)
                                                      .OrderByDescending(g => g.Count())
                                                      .FirstOrDefault()
                                                      ?.FirstOrDefault();

            // Znajdź kategorię z najniższymi wydatkami w okresie
            var leastSpentCategory = categoryExpenses.OrderBy(c => c.TotalExpenses)
                                                     .FirstOrDefault();

            // Pobierz wszystkie kategorie
            var categories = await _categoryRepository.GetAllAsync();

            // Mapuj wydatki kategorii na obiekt z danymi kategorii oraz sumą wydatków
            var categoryExpensesWithDetails = categoryExpenses.Select(c =>
            {
                var category = categories.FirstOrDefault(cat => cat.CategoryId == c.CategoryId);
                return new
                {
                    Category = _mapper.Map<CategoryDto>(category),
                    TotalExpenses = c.TotalExpenses
                };
            }).ToList();

            // Znajdź kategorie, w których nie było żadnych wydatków w okresie
            var categoriesWithNoExpenses = categories.Where(cat => !categoryExpenses.Any(c => c.CategoryId == cat.CategoryId))
                                                     .Select(cat => new
                                                     {
                                                         Category = _mapper.Map<CategoryDto>(cat),
                                                         TotalExpenses = 0.0
                                                     })
                                                     .ToList();

            // Oblicz sumę wydatków z okresu
            var totalExpensesForPeriod = categoryExpenses.Sum(c => c.TotalExpenses);

            // Tworzenie wynikowego obiektu z danymi statystyk
            var result = new
            {
                Period = period.ToString(),
                StartDate = startDate,
                EndDate = endDate,
                MostSpentCategory = categoryExpensesWithDetails.FirstOrDefault(c => c.Category.CategoryId == mostSpentCategory.CategoryId),
                MostFrequentCategory = categoryExpensesWithDetails.FirstOrDefault(c => c.Category.CategoryId == mostFrequentCategory?.CategoryId),
                LeastSpentCategory = categoryExpensesWithDetails.FirstOrDefault(c => c.Category.CategoryId == leastSpentCategory.CategoryId),
                CategoriesWithNoExpenses = categoriesWithNoExpenses,
                TotalExpensesForPeriod = totalExpensesForPeriod,
                CategoryExpensesForPeriod = categoryExpensesWithDetails
            };

            return Ok(result);
        }
        // POST: api/Budgets
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Budget>> PostBudget(BudgetDto budgetdto)
        {
            var budget = _mapper.Map<Budget>(budgetdto);
            await _repository.AddAsync(budget);

            return CreatedAtAction("GetBudget", new { id = budget.BudgetId }, budget);
        }

        // DELETE: api/Budgets/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBudget(int id)
        {
            var budget = await _repository.GetAsync(id);
            if (budget == null)
            {
                return NotFound();
            }
            await _repository.DeleteAsync(id);
            return NoContent();
        }

        private async Task<bool> BudgetExists(int id)
        {
            return await _repository.Exists(id);
        }
    }
}
