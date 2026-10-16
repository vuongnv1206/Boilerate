using System;
using System.Collections.Generic;
using System.Linq;

namespace Boilerate.Application.Common.Exporters;

/// <summary>
/// PDF document options
/// </summary>
public class PdfOptions
{
    /// <summary>
    /// Document title (metadata)
    /// </summary>
    public string Title { get; set; } = "Document";

    /// <summary>
    /// Document author (metadata)
    /// </summary>
    public string Author { get; set; } = "Boilerate System";

    /// <summary>
    /// Document subject (metadata)
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Show page numbers
    /// </summary>
    public bool ShowPageNumbers { get; set; } = true;

    /// <summary>
    /// Include header
    /// </summary>
    public bool IncludeHeader { get; set; } = true;

    /// <summary>
    /// Include footer
    /// </summary>
    public bool IncludeFooter { get; set; } = true;

    /// <summary>
    /// Header text (left side)
    /// </summary>
    public string HeaderLeft { get; set; } = string.Empty;

    /// <summary>
    /// Header text (right side)
    /// </summary>
    public string HeaderRight { get; set; } = string.Empty;

    /// <summary>
    /// Footer text (left side)
    /// </summary>
    public string FooterLeft { get; set; } = string.Empty;

    /// <summary>
    /// Footer text (right side)
    /// </summary>
    public string FooterRight { get; set; } = string.Empty;
}

/// <summary>
/// Invoice data model
/// </summary>
public class InvoiceData
{
    public string InvoiceNumber { get; set; } = default!;
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }
    
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyAddress { get; set; } = string.Empty;
    public string CompanyPhone { get; set; } = string.Empty;
    public string CompanyEmail { get; set; } = string.Empty;
    
    public string CustomerName { get; set; } = default!;
    public string CustomerAddress { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    
    public List<InvoiceItem> Items { get; set; } = new();
    
    public decimal Subtotal => Items.Sum(x => x.Total);
    public decimal TaxRate { get; set; } = 0.1m; // 10%
    public decimal TaxAmount => Subtotal * TaxRate;
    public decimal Total => Subtotal + TaxAmount;
    
    public string Notes { get; set; } = string.Empty;
    public string PaymentTerms { get; set; } = string.Empty;
}

/// <summary>
/// Invoice item
/// </summary>
public class InvoiceItem
{
    public string Description { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total => Quantity * UnitPrice;

    public InvoiceItem() { }

    public InvoiceItem(string description, int quantity, decimal unitPrice)
    {
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}

/// <summary>
/// Report data model
/// </summary>
public class ReportData
{
    public string Title { get; set; } = "Report";
    public string Subtitle { get; set; } = string.Empty;
    public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
    public string GeneratedBy { get; set; } = "System";
    
    public string Summary { get; set; } = string.Empty;
    
    public List<ReportSection> Sections { get; set; } = new();
    
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Report section
/// </summary>
public class ReportSection
{
    public string Title { get; set; } = default!;
    public string Content { get; set; } = string.Empty;
    public List<ReportTable> Tables { get; set; } = new();
    public byte[]? ChartImage { get; set; }
}

/// <summary>
/// Report table
/// </summary>
public class ReportTable
{
    public string Title { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
}

