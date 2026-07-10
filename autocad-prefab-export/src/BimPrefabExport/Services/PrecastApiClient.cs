using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BimPrefabExport.Services;

public sealed class PrecastApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private string? _accessToken;

    public PrecastApiClient(string baseUrl, string? accessToken = null)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');
        _http = new HttpClient { BaseAddress = new Uri(trimmed + "/"), Timeout = TimeSpan.FromSeconds(60) };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        AccessToken = accessToken;
    }

    public string BaseUrl => _http.BaseAddress?.ToString().TrimEnd('/') ?? "";

    public string? AccessToken
    {
        get => _accessToken;
        set
        {
            _accessToken = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            _http.DefaultRequestHeaders.Authorization = _accessToken is null
                ? null
                : new AuthenticationHeaderValue("Bearer", _accessToken);
        }
    }

    public async Task TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _http.GetAsync("api/health", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new PrecastApiException($"Sunucu yanıt vermedi ({(int)response.StatusCode}).");
        }
        catch (HttpRequestException ex)
        {
            throw new PrecastApiException(
                $"Sunucuya bağlanılamadı ({BaseUrl}). Parallels kullanıyorsanız Mac IP adresini girin (ör. http://10.211.55.2:5255). Detay: {ex.Message}");
        }
    }

    public async Task<LoginResponseDto> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new LoginRequestDto(email.Trim(), password), JsonOptions);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        try
        {
            using var response = await _http.PostAsync("api/auth/login", content, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new PrecastApiException(TranslateAuthError(response.StatusCode, body));

            var result = JsonSerializer.Deserialize<LoginResponseDto>(body, JsonOptions)
                         ?? throw new PrecastApiException("Sunucu yanıtı okunamadı.");
            AccessToken = result.AccessToken;
            BimPrefabLog.Info($"Giriş başarılı: {result.Email}");
            return result;
        }
        catch (HttpRequestException ex)
        {
            throw new PrecastApiException($"Bağlantı hatası: {ex.Message}");
        }
    }

    public async Task<LoginResponseDto> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsync("api/auth/refresh", null, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new PrecastApiException("Oturum süresi doldu. Tekrar giriş yapın.");
        if (!response.IsSuccessStatusCode)
            throw new PrecastApiException(ExtractErrorMessage(body, "Token yenilenemedi."));

        var result = JsonSerializer.Deserialize<LoginResponseDto>(body, JsonOptions)
                     ?? throw new PrecastApiException("Sunucu yanıtı okunamadı.");
        AccessToken = result.AccessToken;
        return result;
    }

    public async Task<MeResponseDto> GetMeAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync("api/auth/me", cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new PrecastApiException("Oturum süresi doldu. Tekrar giriş yapın.");
        if (!response.IsSuccessStatusCode)
            throw new PrecastApiException(ExtractErrorMessage(body, $"Kimlik doğrulama hatası ({(int)response.StatusCode})."));

        return JsonSerializer.Deserialize<MeResponseDto>(body, JsonOptions)
               ?? throw new PrecastApiException("Sunucu yanıtı okunamadı.");
    }

    public async Task<IReadOnlyList<ProjectDto>> ListProjectsAsync(CancellationToken cancellationToken = default)
    {
        var list = await ODataListAsync<ProjectDto>(
            "Projects?$orderby=CreatedAtUtc desc&$top=200",
            cancellationToken).ConfigureAwait(false);
        BimPrefabLog.Info($"Proje listesi: {list.Count} kayıt");
        return list;
    }

    public async Task<ProjectDto> CreateProjectAsync(ProjectWriteDto payload, CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("odata/Projects", content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new PrecastApiException(ExtractErrorMessage(body, $"Proje oluşturulamadı ({(int)response.StatusCode})."));

        return JsonSerializer.Deserialize<ProjectDto>(body, JsonOptions)
               ?? throw new PrecastApiException("Sunucu yanıtı okunamadı.");
    }

    public async Task<IReadOnlyList<ProjectProductDto>> ListProjectProductsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var filter = $"ProjectId eq {projectId:D}";
        var all = new List<ProjectProductDto>();
        var skip = 0;
        const int pageSize = 200;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path =
                $"ProjectProducts?$filter={Uri.EscapeDataString(filter)}&$orderby=UpdatedAtUtc desc&$top={pageSize}&$skip={skip}";
            var page = await ODataListAsync<ProjectProductDto>(path, cancellationToken).ConfigureAwait(false);
            if (page.Count == 0)
                break;

            all.AddRange(page);
            if (page.Count < pageSize)
                break;
            skip += pageSize;
        }

        BimPrefabLog.Info($"Proje ürünleri ({projectId:D}): {all.Count} kayıt");
        return all;
    }

    public async Task<ProjectProductDto> CreateProjectProductAsync(
        ProjectProductWriteDto payload,
        CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("odata/ProjectProducts", content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new PrecastApiException(ExtractErrorMessage(body, $"Ürün oluşturulamadı ({(int)response.StatusCode})."));

        return JsonSerializer.Deserialize<ProjectProductDto>(body, JsonOptions)
               ?? throw new PrecastApiException("Sunucu yanıtı okunamadı.");
    }

    public async Task<ProjectProductDto> ReplaceProjectProductAsync(
        Guid productId,
        ProjectProductWriteDto payload,
        CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _http.PutAsync($"odata/ProjectProducts({productId:D})", content, cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new PrecastApiException(ExtractErrorMessage(body, $"Ürün güncellenemedi ({(int)response.StatusCode})."));

        return JsonSerializer.Deserialize<ProjectProductDto>(body, JsonOptions)
               ?? throw new PrecastApiException("Sunucu yanıtı okunamadı.");
    }

    public async Task<ProjectFileUploadResponseDto> UploadProductPdfAsync(
        Guid projectId,
        Guid productId,
        string filePath,
        string? title,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new PrecastApiException($"PDF dosyası bulunamadı: {filePath}");

        await using var stream = File.OpenRead(filePath);
        using var form = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", Path.GetFileName(filePath));
        if (!string.IsNullOrWhiteSpace(title))
            form.Add(new StringContent(title.Trim()), "title");

        using var response = await _http
            .PostAsync($"api/bim/projects/{projectId:D}/products/{productId:D}/pdf", form, cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new PrecastApiException(ExtractErrorMessage(body, $"PDF yüklenemedi ({(int)response.StatusCode})."));

        return JsonSerializer.Deserialize<ProjectFileUploadResponseDto>(body, JsonOptions)
               ?? throw new PrecastApiException("PDF yükleme yanıtı okunamadı.");
    }

    public async Task<ProjectProductCommitResponseDto> CreateProjectProductCommitAsync(
        Guid projectId,
        ProjectProductCommitRequestDto payload,
        CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _http
            .PostAsync($"api/bim/projects/{projectId:D}/commits", content, cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new PrecastApiException(ExtractErrorMessage(body, $"Commit gönderilemedi ({(int)response.StatusCode})."));

        return JsonSerializer.Deserialize<ProjectProductCommitResponseDto>(body, JsonOptions)
               ?? throw new PrecastApiException("Commit yanıtı okunamadı.");
    }

    /// <summary>Frontend <c>fetchElementIdentityCatalog</c> ile aynı OData uçları.</summary>
    public async Task<ElementIdentityCatalogBundle> FetchElementIdentityCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        var categoriesTask = ODataListAsync<CatalogCategoryApiDto>(
            "CatalogElementCategories?$orderby=sortOrder asc", cancellationToken);
        var typesTask = ODataListAsync<CatalogElementTypeApiDto>(
            "CatalogElementTypes?$orderby=sortOrder asc", cancellationToken);
        var typosTask = ODataListAsync<CatalogTypologyApiDto>(
            "CatalogTypologies?$expand=dimensions&$orderby=code asc", cancellationToken);
        var dimsTask = ODataListAsync<CatalogIdentifyingDimensionApiDto>(
            "CatalogIdentifyingDimensions?$orderby=code asc", cancellationToken);

        await Task.WhenAll(categoriesTask, typesTask, typosTask, dimsTask).ConfigureAwait(false);

        var bundle = new ElementIdentityCatalogBundle
        {
            Categories = categoriesTask.Result
                .Select(c => new CatalogCategoryDto
                {
                    Code = c.Code,
                    NameTr = c.NameTr,
                    NameEn = c.NameEn,
                    SortOrder = c.SortOrder,
                })
                .ToList(),
            ElementTypes = typesTask.Result
                .Select(t => new CatalogElementTypeDto
                {
                    Code = t.Code,
                    Category = t.Category,
                    NameTr = t.NameTr,
                    NameEn = t.NameEn,
                    SortOrder = t.SortOrder,
                })
                .ToList(),
            Typologies = typosTask.Result
                .Select(t => new CatalogTypologyDto
                {
                    Code = t.Code,
                    ElementTypeCode = t.ElementTypeCode,
                    NameTr = t.NameTr,
                    NameEn = t.NameEn,
                    ShowInUserFilter = t.ShowInUserFilter,
                    DimensionCodes = (t.Dimensions ?? [])
                        .OrderBy(d => d.SortOrder)
                        .Select(d => d.DimensionCode)
                        .Where(c => !string.IsNullOrWhiteSpace(c))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    ShowInUserFilter = t.ShowInUserFilter,
                })
                .ToList(),
            IdentifyingDimensions = dimsTask.Result
                .Select(d => new CatalogIdentifyingDimensionDto
                {
                    Code = d.Code,
                    NameTr = d.NameTr,
                    NameEn = d.NameEn,
                    UnitCategoryCode = string.IsNullOrWhiteSpace(d.UnitCategoryCode) ? "length" : d.UnitCategoryCode,
                    Unit = d.Unit,
                })
                .ToList(),
        };

        BimPrefabLog.Info(
            $"Element identity katalog: {bundle.Categories.Count} kategori, {bundle.ElementTypes.Count} tip, {bundle.Typologies.Count} tipoloji, {bundle.IdentifyingDimensions.Count} boyut.");
        return bundle;
    }

    public async Task<IReadOnlyList<FirmTypologySettingEntry>> FetchFirmTypologySettingsAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await _http
            .GetAsync("api/bim/firm-typology-settings", cancellationToken)
            .ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new PrecastApiException("Oturum süresi doldu. Tekrar giriş yapın.");
        if (!response.IsSuccessStatusCode)
            throw new PrecastApiException(ExtractErrorMessage(body, $"Firma tipoloji ayarları alınamadı ({(int)response.StatusCode})."));

        var rows = JsonSerializer.Deserialize<List<FirmTypologySettingApiDto>>(body, JsonOptions) ?? [];
        return rows
            .Select(r => new FirmTypologySettingEntry
            {
                TypologyCode = r.TypologyCode,
                IdentifyingDimensionCodes = r.IdentifyingDimensionCodes?.ToList() ?? [],
                ShowInUserFilterOverride = r.ShowInUserFilterOverride,
            })
            .ToList();
    }

    private async Task<List<T>> ODataListAsync<T>(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.GetAsync("odata/" + path, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new PrecastApiException("Oturum süresi doldu. Tekrar giriş yapın.");
            if (!response.IsSuccessStatusCode)
                throw new PrecastApiException(ExtractErrorMessage(body, $"OData isteği başarısız ({(int)response.StatusCode})."));

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                return JsonSerializer.Deserialize<List<T>>(body, JsonOptions) ?? [];

            if (doc.RootElement.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
                return JsonSerializer.Deserialize<List<T>>(value.GetRawText(), JsonOptions) ?? [];

            return [];
        }
        catch (HttpRequestException ex)
        {
            throw new PrecastApiException($"Bağlantı hatası: {ex.Message}");
        }
    }

    private static string TranslateAuthError(System.Net.HttpStatusCode status, string body)
    {
        if (status == System.Net.HttpStatusCode.Unauthorized)
            return "E-posta veya şifre hatalı.";
        return ExtractErrorMessage(body, "Giriş başarısız.");
    }

    public static string ExtractErrorMessage(string body, string fallback)
    {
        if (string.IsNullOrWhiteSpace(body))
            return fallback;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                return msg.GetString() ?? fallback;
            if (doc.RootElement.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object
                && err.TryGetProperty("message", out var inner) && inner.ValueKind == JsonValueKind.String)
                return inner.GetString() ?? fallback;
        }
        catch
        {
            // ignore
        }

        return body.Length > 240 ? body[..240] + "…" : body;
    }

    public void Dispose() => _http.Dispose();
}

public sealed class PrecastApiException : Exception
{
    public PrecastApiException(string message) : base(message) { }
}

public sealed record LoginRequestDto(string Email, string Password);

public sealed class LoginResponseDto
{
    public string AccessToken { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
}

public sealed class MeResponseDto
{
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
}

public sealed class ProjectDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string FactoryCode { get; set; } = "";
    public string Status { get; set; } = "";
}

public sealed class ProjectWriteDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string FactoryCode { get; set; } = "IST-HAD";
    public string Status { get; set; } = "tasarim";
}

public sealed class ProjectProductDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string ElementTypeCode { get; set; } = "";
    public string TypologyCode { get; set; } = "";
    public string Source { get; set; } = "";
    public string LifecycleStatus { get; set; } = "";
    public decimal VolumeCubicM { get; set; }
    public int Quantity { get; set; } = 1;
    public int? ProductionSequence { get; set; }
    public string DimensionsJson { get; set; } = "{}";
    public string Notes { get; set; } = "";
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
}

public sealed class ProjectProductWriteDto
{
    public Guid? Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string ElementTypeCode { get; set; } = "";
    public string TypologyCode { get; set; } = "";
    public string Source { get; set; } = "cad";
    public string LifecycleStatus { get; set; } = "tasarim";
    public decimal VolumeCubicM { get; set; }
    public int Quantity { get; set; } = 1;
    public int? ProductionSequence { get; set; }
    public string DimensionsJson { get; set; } = "{}";
    public string Notes { get; set; } = "";
}

public sealed class ProjectFileUploadResponseDto
{
    public Guid Id { get; set; }
    public string Url { get; set; } = "";
    public string Name { get; set; } = "";
    public long SizeBytes { get; set; }
}

internal sealed class CatalogCategoryApiDto
{
    public string Code { get; set; } = "";
    public string NameTr { get; set; } = "";
    public string? NameEn { get; set; }
    public int SortOrder { get; set; }
}

internal sealed class CatalogElementTypeApiDto
{
    public string Code { get; set; } = "";
    public string Category { get; set; } = "";
    public string NameTr { get; set; } = "";
    public string? NameEn { get; set; }
    public int SortOrder { get; set; }
}

internal sealed class CatalogTypologyApiDto
{
    public string Code { get; set; } = "";
    public string ElementTypeCode { get; set; } = "";
    public string NameTr { get; set; } = "";
    public string? NameEn { get; set; }
    public bool ShowInUserFilter { get; set; } = true;
    public List<CatalogTypologyDimensionApiDto>? Dimensions { get; set; }
}

internal sealed class FirmTypologySettingApiDto
{
    public string TypologyCode { get; set; } = "";
    public List<string>? IdentifyingDimensionCodes { get; set; }
    public bool? ShowInUserFilterOverride { get; set; }
}

internal sealed class CatalogTypologyDimensionApiDto
{
    public string DimensionCode { get; set; } = "";
    public int SortOrder { get; set; }
}

internal sealed class CatalogIdentifyingDimensionApiDto
{
    public string Code { get; set; } = "";
    public string NameTr { get; set; } = "";
    public string? NameEn { get; set; }
    public string UnitCategoryCode { get; set; } = "length";
    public string Unit { get; set; } = "mm";
}

public sealed class ProjectProductCommitRequestDto
{
    public string Message { get; set; } = "";
    public string Source { get; set; } = "cad";
    public List<ProjectProductCommitChangeRequestDto> Changes { get; set; } = new();
}

public sealed class ProjectProductCommitChangeRequestDto
{
    public string? CadProductId { get; set; }
    public string ProductCode { get; set; } = "";
    public string ChangeType { get; set; } = "updated";
    public string ChangedFieldsJson { get; set; } = "[]";
    public int RevisionBefore { get; set; }
    public int RevisionAfter { get; set; }
    public string? ContentHashBefore { get; set; }
    public string ContentHashAfter { get; set; } = "";
    public ProjectProductWriteDto Product { get; set; } = new();
}

public sealed class ProjectProductCommitResponseDto
{
    public Guid CommitId { get; set; }
    public List<ProjectProductCommitChangeResultDto> Changes { get; set; } = new();
}

public sealed class ProjectProductCommitChangeResultDto
{
    public Guid? ProductId { get; set; }
    public string? CadProductId { get; set; }
    public string ProductCode { get; set; } = "";
    public string ChangeType { get; set; } = "";
}
