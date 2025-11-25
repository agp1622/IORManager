import { useEffect, useId, useState } from 'react'

const createEmptyLine = (unitOfMeasure = 'unit') => ({
  description: '',
  quantity: '1',
  unitPrice: '0',
  unitOfMeasure,
})

const currencyOptions = ['USD', 'DOP']

const unitOptions = [
  'unit',
  'kg',
  'g',
  't',
  'm',
  'cm',
  'mm',
  'km',
  'm2',
  'm3',
  'l',
  'ml',
  'lb',
  'oz',
  'ft',
  'in',
  'yd',
  'gal',
]

const InvoiceForm = ({
  onSubmit,
  isSubmitting,
  t = (value) => value,
  defaultCurrency = 'USD',
  locale = 'en-US',
  initialInvoice = null,
  mode = 'create',
  onCancelEdit,
}) => {
  const today = new Date().toISOString().split('T')[0]
  const formInstanceId = useId()
  const [invoiceDate, setInvoiceDate] = useState(today)
  const [customerName, setCustomerName] = useState('')
  const [currencyCode, setCurrencyCode] = useState(defaultCurrency)
  const [itbisRate, setItbisRate] = useState('')
  const [lines, setLines] = useState([createEmptyLine()])
  const isEditMode = mode === 'edit' && initialInvoice

  useEffect(() => {
    if (initialInvoice) {
      const normalizedDate = initialInvoice.date
        ? new Date(initialInvoice.date).toISOString().split('T')[0]
        : today
      setInvoiceDate(normalizedDate)
      setCustomerName(initialInvoice.customerName || initialInvoice.partyName || '')
      setCurrencyCode(initialInvoice.currencyCode || defaultCurrency)
      setItbisRate(
        typeof initialInvoice.itbisRate === 'number'
          ? (Number(initialInvoice.itbisRate) * 100).toString()
          : '',
      )
      const nextLines = Array.isArray(initialInvoice.lines) && initialInvoice.lines.length > 0
        ? initialInvoice.lines.map((line) => ({
            description: line.description ?? '',
            quantity: String(line.quantity ?? 1),
            unitPrice:
              typeof line.unitPrice === 'number'
                ? Number(line.unitPrice).toString()
                : line.unitPrice || '0',
            unitOfMeasure: line.unitOfMeasure || 'unit',
          }))
        : [createEmptyLine()]
      setLines(nextLines)
      return
    }

    setInvoiceDate(today)
    setCustomerName('')
    setCurrencyCode(defaultCurrency)
    setItbisRate('')
    setLines([createEmptyLine()])
  }, [initialInvoice, defaultCurrency, today])

  const updateLine = (index, field, value) => {
    setLines((current) =>
      current.map((line, lineIndex) =>
        lineIndex === index ? { ...line, [field]: value } : line,
      ),
    )
  }

  const addLine = () => {
    setLines((current) => {
      const lastUnit = current[current.length - 1]?.unitOfMeasure ?? 'unit'
      return [...current, createEmptyLine(lastUnit)]
    })
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

    const normalizedItbisRate = (() => {
      const parsed = Number(itbisRate)
      if (!Number.isFinite(parsed) || parsed <= 0) {
        return null
      }
      const capped = Math.min(parsed, 100)
      return capped / 100
    })()

    const payload = {
      invoiceDate,
      customerName,
      currencyCode,
      locale: initialInvoice?.cultureName || locale,
      itbisRate: normalizedItbisRate,
      lines: lines.map((line) => ({
        description: line.description,
        quantity: Number(line.quantity) || 0,
        unitPrice: Number(line.unitPrice) || 0,
        unitOfMeasure: line.unitOfMeasure,
      })),
    }

    const wasSuccessful = await onSubmit(payload)

    if (wasSuccessful && !isEditMode) {
      setInvoiceDate(today)
      setCustomerName('')
      setCurrencyCode(defaultCurrency)
      setItbisRate('')
      setLines([createEmptyLine()])
    }
  }

  return (
    <form className="document-form" onSubmit={handleSubmit}>
      {isEditMode && initialInvoice?.number ? (
        <div className="form-hint form-hint--warning">
          {t('invoiceForm.editingLabel')}{' '}
          <span className="form-hint__strong">{initialInvoice.number}</span>
        </div>
      ) : null}
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
        <label>
          {t('invoiceForm.itbisLabel')}
          <input
            type="number"
            min="0"
            max="100"
            step="0.01"
            value={itbisRate}
            onChange={(event) => setItbisRate(event.target.value)}
            placeholder={t('invoiceForm.itbisPlaceholder')}
          />
          <p className="input-hint">{t('invoiceForm.itbisHint')}</p>
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
        <div className="line-table-wrapper">
          <table className="line-table">
            <thead>
              <tr>
                <th scope="col">{t('invoiceForm.descriptionLabel')}</th>
                <th scope="col">{t('invoiceForm.quantityLabel')}</th>
                <th scope="col">{t('invoiceForm.unitOfMeasureLabel')}</th>
                <th scope="col">{t('invoiceForm.unitPriceLabel')}</th>
                <th scope="col" className="line-table__actions-heading">
                  {t('invoiceForm.removeLine')}
                </th>
              </tr>
            </thead>
            <tbody>
              {lines.map((line, index) => {
                const idPrefix = `${formInstanceId}-line-${index}`
                return (
                  <tr key={`line-${index}`}>
                    <td data-heading={t('invoiceForm.descriptionLabel')}>
                      <label className="sr-only" htmlFor={`${idPrefix}-description`}>
                        {t('invoiceForm.descriptionLabel')}
                      </label>
                      <textarea
                        id={`${idPrefix}-description`}
                        rows="2"
                        value={line.description}
                        onChange={(event) =>
                          updateLine(index, 'description', event.target.value)
                        }
                        required
                        placeholder={t('invoiceForm.descriptionPlaceholder')}
                        className="line-table__textarea"
                      />
                    </td>
                    <td data-heading={t('invoiceForm.quantityLabel')}>
                      <label className="sr-only" htmlFor={`${idPrefix}-quantity`}>
                        {t('invoiceForm.quantityLabel')}
                      </label>
                      <input
                        id={`${idPrefix}-quantity`}
                        type="number"
                        min="1"
                        value={line.quantity}
                        onChange={(event) => updateLine(index, 'quantity', event.target.value)}
                        required
                        className="line-table__input"
                      />
                    </td>
                    <td data-heading={t('invoiceForm.unitOfMeasureLabel')}>
                      <label className="sr-only" htmlFor={`${idPrefix}-unit`}>
                        {t('invoiceForm.unitOfMeasureLabel')}
                      </label>
                      <select
                        id={`${idPrefix}-unit`}
                        value={line.unitOfMeasure}
                        onChange={(event) => updateLine(index, 'unitOfMeasure', event.target.value)}
                        required
                        className="line-table__input"
                      >
                        {unitOptions.map((code) => (
                          <option key={code} value={code}>
                            {t(`units.${code}`)}
                          </option>
                        ))}
                      </select>
                    </td>
                    <td data-heading={t('invoiceForm.unitPriceLabel')}>
                      <label className="sr-only" htmlFor={`${idPrefix}-price`}>
                        {t('invoiceForm.unitPriceLabel')}
                      </label>
                      <input
                        id={`${idPrefix}-price`}
                        type="number"
                        min="0"
                        step="0.01"
                        value={line.unitPrice}
                        onChange={(event) => updateLine(index, 'unitPrice', event.target.value)}
                        required
                        className="line-table__input"
                      />
                    </td>
                    <td className="line-table__actions" data-heading={t('invoiceForm.removeLine')}>
                      <button
                        type="button"
                        className="button button--icon"
                        onClick={() => removeLine(index)}
                        aria-label={t('invoiceForm.removeLine')}
                        disabled={lines.length === 1}
                      >
                        ×
                      </button>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      </div>

      <div className="form-actions">
        {isEditMode && onCancelEdit ? (
          <button
            type="button"
            className="button button--ghost"
            onClick={onCancelEdit}
            disabled={isSubmitting}
          >
            {t('invoiceForm.cancelEdit')}
          </button>
        ) : null}
        <button type="submit" className="button" disabled={isSubmitting}>
          {isSubmitting
            ? isEditMode
              ? t('invoiceForm.updating')
              : t('invoiceForm.submitting')
            : isEditMode
              ? t('invoiceForm.updateSubmit')
              : t('invoiceForm.submit')}
        </button>
      </div>
    </form>
  )
}

export default InvoiceForm
