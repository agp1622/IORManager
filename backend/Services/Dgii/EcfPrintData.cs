namespace IORManager.Services.Dgii;

/// <summary>
/// Data needed to print the DGII verification QR code and security code on an e-CF's Representación
/// Impresa (RI) — the printed/PDF copy handed to a non-electronic recipient.
/// </summary>
public sealed record EcfPrintData(string ENcf, string SecurityCode, DateTime SignedAtUtc, byte[] QrCodePng);
