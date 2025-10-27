import { useEffect, useState } from 'react'

const createEmptyLine = () => ({
  description: '',
  quantity: '1',
  unitPrice: '0',
})

const currencyOptions = ['USD', 'DOP']

const InvoiceForm = ({
  onSubmit,
  isSubmitting,
  t = (value) => value,
  defaultCurrency = 'USD',
  locale = 'en-US',
}) => {
  const today = new Date().toISOString().split('T')[0]
  const [invoiceDate, setInvoiceDate] = useState(today)
  const [customerName, setCustomerName] = useState('')
  const [currencyCode, setCurrencyCode] = useState(defaultCurrency)
  const [lines, setLines] = useState([createEmptyLine()])

  useEffect(() => {
    setCurrencyCode(defaultCurrency)
  }, [defaultCurrency])

  const updateLine = (index, field, value) => {
    setLines((current) =>
      current.map((line, lineIndex) =>
        lineIndex === index ? { ...line, [field]: value } : line,
      ),
    )
  }

  const addLine = () => {
    setLines((current) => [...current, createEmptyLine()])
  }

  const removeLine = (index) => {
    setLines((current) =>
      current.length > 1
        ? current.filter((_, lineIndex) => lineIndex !== index)
        : current,
    )
  }

  const handleSubmit = async (event) => {
    event.preventDefault()

    const payload = {
      invoiceDate,
      customerName,
      currencyCode,
      locale,
      lines: lines.map((line) => ({
        description: line.description,
        quantity: Number(line.quantity) || 0,
        unitPrice: Number(line.unitPrice) || 0,
      })),
    }

    const wasSuccessful = await onSubmit(payload)

    if (wasSuccessful) {
      setInvoiceDate(today)
      setCustomerName('')
      setCurrencyCode(defaultCurrency)
      setLines([createEmptyLine()])
    }
  }

  return (
    <form className="document-form" onSubmit={handleSubmit}>
      <div className="field-grid">
        <label>
          {t('invoiceForm.invoiceDateLabel')}
          <input
            type="date"
            value={invoiceDate}
            onChange={(event) => setInvoiceDate(event.target.value)}
            required
          />
        </label>
        <label>
          {t('invoiceForm.customerNameLabel')}
          <input
            type="text"
            value={customerName}
            onChange={(event) => setCustomerName(event.target.value)}
            required
            placeholder={t('invoiceForm.customerNamePlaceholder')}
          />
        </label>
        <label>
          {t('invoiceForm.currencyLabel')}
          <select
            value={currencyCode}
            onChange={(event) => setCurrencyCode(event.target.value)}
            required
          >
            {currencyOptions.map((code) => (
              <option key={code} value={code}>
                {t(`currencies.${code}`)}
              </option>
            ))}
          </select>
        </label>
      </div>

      <div className="form-section">
        <div className="form-section__header">
          <h4>{t('invoiceForm.lineItemsHeading')}</h4>
          <button
            type="button"
            className="button button--secondary"
            onClick={addLine}
          >
            {t('invoiceForm.addLine')}
          </button>
        </div>
        {lines.map((line, index) => (
          <div key={index} className="line-row">
            <label>
              {t('invoiceForm.descriptionLabel')}
              <input
                type="text"
                value={line.description}
                onChange={(event) =>
                  updateLine(index, 'description', event.target.value)
                }
                required
                placeholder={t('invoiceForm.descriptionPlaceholder')}
              />
            </label>
            <label>
              {t('invoiceForm.quantityLabel')}
              <input
                type="number"
                min="1"
                value={line.quantity}
                onChange={(event) => updateLine(index, 'quantity', event.target.value)}
                required
              />
            </label>
            <label>
              {t('invoiceForm.unitPriceLabel')}
              <input
                type="number"
                min="0"
                step="0.01"
                value={line.unitPrice}
                onChange={(event) => updateLine(index, 'unitPrice', event.target.value)}
                required
              />
            </label>
            <button
              type="button"
              className="button button--icon"
              onClick={() => removeLine(index)}
              aria-label={t('invoiceForm.removeLine')}
              disabled={lines.length === 1}
            >
              ×
            </button>
          </div>
        ))}
      </div>

      <button type="submit" className="button" disabled={isSubmitting}>
        {isSubmitting ? t('invoiceForm.submitting') : t('invoiceForm.submit')}
      </button>
    </form>
  )
}

export default InvoiceForm
