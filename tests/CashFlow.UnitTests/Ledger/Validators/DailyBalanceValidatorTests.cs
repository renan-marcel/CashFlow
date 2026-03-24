using Bogus;
using CashFlow.Domain.Ledger;
using CashFlow.Domain.Ledger.Validators;
using FluentAssertions;

namespace CashFlow.UnitTests.Ledger.Validators;

/// <summary>
/// Unit tests para o validador DailyBalanceValidator
/// Testes estressados focados especificamente na validação de regras de negócio
/// </summary>
public class DailyBalanceValidatorTests
{
    private readonly DailyBalanceValidator _validator;

    public DailyBalanceValidatorTests()
    {
        _validator = new DailyBalanceValidator();
    }

    #region Valid Scenarios

    [Fact]
    public void ValidateCompleteBalance_WithAllValidFields_ShouldSucceed()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5))
        );

        // Act
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateBalance_WithZeroBalance_ShouldSucceed()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        // Act
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateBalance_WithPositiveBalance_ShouldSucceed()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );
        balance.ApplyCredit(500m);

        // Act
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateBalance_WithCurrentDate_ShouldSucceed()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        // Act
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateBalance_WithPastDate_ShouldSucceed()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30))
        );

        // Act
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateBalance_WithVeryOldDate_ShouldSucceed()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: new DateOnly(2000, 1, 1)
        );

        // Act
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region Invalid Scenarios - Individual Field Failures

    [Fact]
    public void ValidateBalance_WithEmptyMerchantId_ShouldFail()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.Empty,
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        // Act
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(DailyBalance.MerchantId));
    }

    [Fact]
    public void ValidateBalance_WithFutureDate_ShouldFail()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))
        );

        // Act
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(DailyBalance.Date));
    }

    [Fact]
    public void ValidateBalance_WithNegativeBalance_ShouldFail()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );
        balance.ApplyDebit(100m);

        // Act
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(DailyBalance.Balance));
    }

    [Fact]
    public void ValidateBalance_WithFutureUpdatedAtUtc_ShouldFail()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        // Manually set future timestamp via reflection since setter is private
        var propertyInfo = typeof(DailyBalance).GetProperty(nameof(DailyBalance.UpdatedAtUtc));
        propertyInfo?.SetValue(balance, DateTime.UtcNow.AddDays(1));

        // Act
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(DailyBalance.UpdatedAtUtc));
    }

    #endregion

    #region Multiple Error Scenarios

    [Fact]
    public void ValidateBalance_WithMultipleInvalidFields_ShouldReportAllErrors()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.Empty,
            date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))
        );
        balance.ApplyDebit(100m);

        // Manually set future timestamp
        var propertyInfo = typeof(DailyBalance).GetProperty(nameof(DailyBalance.UpdatedAtUtc));
        propertyInfo?.SetValue(balance, DateTime.UtcNow.AddDays(1));

        // Act
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void ValidateBalance_WithPartialInvalidFields_ShouldReportOnlyInvalidOnes()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );
        balance.ApplyDebit(50m);

        // Act
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(DailyBalance.Balance));
    }

    #endregion

    #region Boundary Value Tests

    [Fact]
    public void ValidateBalance_WithMinimalPositiveBalance_ShouldPass()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );
        balance.ApplyCredit(0.001m);

        // Act
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateBalance_WithVeryLargeBalance_ShouldPass()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );
        balance.ApplyCredit((decimal)Math.Pow(10, 10));

        // Act
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    [InlineData(365)]
    [InlineData(3650)]
    public void ValidateBalance_WithVariousPastDateOffsets_ShouldPass(int daysInPast)
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-daysInPast))
        );

        // Act
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidateBalance_WithMinimalNegativeBalance_ShouldFail()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );
        balance.ApplyDebit(0.001m);

        // Act
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region Stress Tests with Bogus

    [Fact]
    public void ValidateMultipleRandomBalances_AllValidData_ShouldPassAll()
    {
        // Arrange
        var faker = new Faker<DailyBalance>()
            .CustomInstantiator(f => new DailyBalance(
                merchantId: Guid.NewGuid(),
                date: DateOnly.FromDateTime(f.Date.PastDateOnly(365).ToDateTime(TimeOnly.MinValue))
            ));

        var balances = faker.Generate(100);

        // Act & Assert
        foreach (var balance in balances)
        {
            var result = _validator.Validate(balance);
            result.IsValid.Should().BeTrue($"Balance {balance.Id} should be valid");
        }
    }

    [Fact]
    public void ValidateRandomBalances_WithRandomInvalidData_ShouldIdentifyErrors()
    {
        // Arrange
        var random = new Random(42);
        var testCount = 100;
        var invalidCount = 0;

        // Act
        for (int i = 0; i < testCount; i++)
        {
            var shouldBeInvalid = random.NextDouble() > 0.7;
            
            var balance = new DailyBalance(
                merchantId: shouldBeInvalid && random.NextDouble() > 0.5 ? Guid.Empty : Guid.NewGuid(),
                date: shouldBeInvalid && random.NextDouble() > 0.5 
                    ? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))
                    : DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-random.Next(0, 365)))
            );

            if (shouldBeInvalid && random.NextDouble() > 0.5)
            {
                balance.ApplyDebit(100m);
            }

            var result = _validator.Validate(balance);
            if (!result.IsValid)
            {
                invalidCount++;
            }
        }

        // Assert
        invalidCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ValidateBalances_WithVariousMerchantAndDateCombinations_ShouldHandleCorrectly()
    {
        // Arrange
        var merchantIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        var dates = Enumerable.Range(0, 30)
            .Select(i => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i)))
            .ToList();

        var balances = new List<DailyBalance>();
        foreach (var merchantId in merchantIds)
        {
            foreach (var date in dates)
            {
                balances.Add(new DailyBalance(merchantId, date));
            }
        }

        // Act & Assert
        balances.Should().HaveCount(150);
        foreach (var balance in balances)
        {
            var result = _validator.Validate(balance);
            result.IsValid.Should().BeTrue();
        }
    }

    [Fact]
    public void ValidateBalances_WithVariousOperationCounts_ShouldMaintainValidity()
    {
        // Arrange
        var random = new Random(42);
        var balances = new List<DailyBalance>();

        for (int i = 0; i < 50; i++)
        {
            var balance = new DailyBalance(
                merchantId: Guid.NewGuid(),
                date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-random.Next(0, 30)))
            );

            // Apply random operations
            var operationCount = random.Next(0, 50);
            for (int j = 0; j < operationCount; j++)
            {
                var amount = (decimal)random.NextDouble() * 1000 + 0.01m;
                if (random.NextDouble() > 0.5)
                {
                    balance.ApplyCredit(amount);
                }
                else
                {
                    // Only debit if balance is sufficient
                    if (balance.Balance >= amount)
                    {
                        balance.ApplyDebit(amount);
                    }
                }
            }

            balances.Add(balance);
        }

        // Act & Assert
        foreach (var balance in balances)
        {
            var result = _validator.Validate(balance);
            result.IsValid.Should().BeTrue($"Balance {balance.Id} should be valid");
        }
    }

    [Fact]
    public void ValidateBalances_WithAllFieldVariations_ShouldHandleCorrectly()
    {
        // Arrange
        var results = new List<bool>();
        
        var merchantIds = new[] { Guid.Empty, Guid.NewGuid(), Guid.NewGuid() };
        var dates = new[]
        {
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30))
        };

        var validCount = 0;
        var invalidCount = 0;

        // Act & Assert
        foreach (var merchantId in merchantIds)
        {
            foreach (var date in dates)
            {
                var balance = new DailyBalance(merchantId, date);
                
                var result = _validator.Validate(balance);
                if (result.IsValid)
                    validCount++;
                else
                    invalidCount++;
            }
        }

        validCount.Should().BeGreaterThan(0);
        invalidCount.Should().BeGreaterThan(0);
    }

    #endregion

    #region Balance Operations and Validation

    [Fact]
    public void ValidateBalance_AfterSingleCredit_ShouldPass()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        // Act
        balance.ApplyCredit(1000m);
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeTrue();
        balance.Balance.Should().Be(1000m);
    }

    [Fact]
    public void ValidateBalance_AfterMultipleCredits_ShouldPass()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        // Act
        balance.ApplyCredit(500m);
        balance.ApplyCredit(300m);
        balance.ApplyCredit(200m);
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeTrue();
        balance.Balance.Should().Be(1000m);
    }

    [Fact]
    public void ValidateBalance_AfterCreditAndValidDebit_ShouldPass()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        // Act
        balance.ApplyCredit(1000m);
        balance.ApplyDebit(600m);
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeTrue();
        balance.Balance.Should().Be(400m);
    }

    [Fact]
    public void ValidateBalance_AfterDebitMakingNegative_ShouldFail()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );
        balance.ApplyCredit(100m);

        // Act
        balance.ApplyDebit(200m);
        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeFalse();
        balance.Balance.Should().Be(-100m);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void ValidateLargeNumberOfBalances_ShouldCompleteInReasonableTime()
    {
        // Arrange
        var faker = new Faker<DailyBalance>()
            .CustomInstantiator(f => new DailyBalance(
                merchantId: Guid.NewGuid(),
                date: DateOnly.FromDateTime(f.Date.PastDateOnly(365).ToDateTime(TimeOnly.MinValue))
            ));

        var balances = faker.Generate(10000);
        var startTime = DateTime.UtcNow;

        // Act
        var validCount = 0;
        foreach (var balance in balances)
        {
            if (_validator.Validate(balance).IsValid)
                validCount++;
        }

        var duration = DateTime.UtcNow - startTime;

        // Assert
        validCount.Should().Be(10000);
        duration.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void ValidateBalancesWithOperations_ShouldCompleteInReasonableTime()
    {
        // Arrange
        var random = new Random(42);
        var balances = new List<DailyBalance>();

        for (int i = 0; i < 1000; i++)
        {
            var balance = new DailyBalance(
                merchantId: Guid.NewGuid(),
                date: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-random.Next(0, 365)))
            );

            // Apply multiple operations
            for (int j = 0; j < 10; j++)
            {
                var amount = (decimal)random.NextDouble() * 100 + 0.01m;
                if (random.NextDouble() > 0.5)
                {
                    balance.ApplyCredit(amount);
                }
                else if (balance.Balance >= amount)
                {
                    balance.ApplyDebit(amount);
                }
            }

            balances.Add(balance);
        }

        var startTime = DateTime.UtcNow;

        // Act
        var validCount = 0;
        foreach (var balance in balances)
        {
            if (_validator.Validate(balance).IsValid)
                validCount++;
        }

        var duration = DateTime.UtcNow - startTime;

        // Assert
        validCount.Should().Be(1000);
        duration.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ValidateBalance_AcrossDifferentYears_ShouldPass()
    {
        // Arrange
        var dates = new[]
        {
            new DateOnly(2020, 1, 1),
            new DateOnly(2021, 6, 15),
            new DateOnly(2022, 12, 31),
            new DateOnly(2023, 3, 10),
            new DateOnly(2024, 9, 25)
        };

        // Act & Assert
        foreach (var date in dates)
        {
            var balance = new DailyBalance(Guid.NewGuid(), date);
            var result = _validator.Validate(balance);
            result.IsValid.Should().BeTrue();
        }
    }

    [Fact]
    public void ValidateBalance_WithPrecisionOperations_ShouldMaintainAccuracy()
    {
        // Arrange
        var balance = new DailyBalance(
            merchantId: Guid.NewGuid(),
            date: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        // Act
        balance.ApplyCredit(10.25m);
        balance.ApplyCredit(20.50m);
        balance.ApplyCredit(30.75m);
        balance.ApplyDebit(15.10m);
        balance.ApplyDebit(22.40m);

        var result = _validator.Validate(balance);

        // Assert
        result.IsValid.Should().BeTrue();
        balance.Balance.Should().Be(24.00m);
    }

    #endregion
}
