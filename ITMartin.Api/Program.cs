using ITMartin.Ai.Interfaces;
using ITMartin.Ai.Services;
using ITMartin.Magic.Application;
using ITMartin.Magic.Application.Interfaces;
using ITMartin.Magic.Infrastructure.Services;
using ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;
using ITMartin.Media.Infrastructure.Services;
using ITMartin.OCR.Interfaces;
using ITMartin.OCR.Services;
using ITMartin.Receipt.Application;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(
    options =>
    {
        options.SwaggerDoc(
            "v1",
            new OpenApiInfo
            {
                Title = "ITMartin API",
                Version = "v1"
            });
    });

// OCR
builder.Services.AddScoped<
    IGeneralOcrService,
    GeneralOcrService>();
builder.Services.AddMagicApplication();
// =========================
// OCR
// =========================

builder.Services.AddScoped<
    IOcrService,
    OcrService>();

// =========================
// AI
// =========================

builder.Services.AddScoped<
    IImageAnalysisService,
    OpenAiImageAnalysisService>();

builder.Services.AddScoped<
    ICardConditionAnalysisService,
    OpenAiCardConditionService>();
builder.Services.AddScoped<
    IMagicCardRecognitionService,
    OpenAiMagicCardRecognitionService>();

// =========================
// OPENCV
// =========================

builder.Services.AddScoped<
    ICardLayoutDetectionService,
    CardLayoutDetectionService>();

builder.Services.AddScoped<
    ICardCornerDetectionService,
    OpenCvCardCornerDetectionService>();

builder.Services.AddScoped<
    IPerspectiveCorrectionService,
    OpenCvPerspectiveCorrectionService>();

builder.Services.AddScoped<
    IBlurDetectionService,
    OpenCvBlurDetectionService>();

builder.Services.AddScoped<
    IOcrRegionExtractor,
    OpenCvMagicCardOcrRegionExtractor>();

// AI
builder.Services.AddScoped<
    IReceiptExtractionService,
    OpenAiReceiptExtractionService>();
builder.Services.AddScoped<
    IReceiptExtractionService,
    OpenAiReceiptExtractionService>();
// Receipt
builder.Services.AddReceiptApplication();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();