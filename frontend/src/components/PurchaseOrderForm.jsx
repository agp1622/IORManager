import { useEffect, useId, useMemo, useState } from 'react'

const createEmptyLine = (unitOfMeasure = 'unit') => ({
  description: '',
  quantity: '1',
  unitPrice: '0',
  unitOfMeasure,
})

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

const CURRENCY_CODES = ['USD', 'DOP']

const PurchaseOrderForm = ({
  onSubmit,
  isSubmitting,
  t = (value) => value,
  quotes = [],
  initialPO = null,
  mode = 'create',
  onCancelEdit,
  prefillQuoteId = null,
}) => {
  const today = new Date().toISOString().split('T')[0]
  const formInstanceId = useId()
  const quoteListId = `${formInstanceId}-quotes`
  const isEditMode = mode === 'edit' && initialPO
  const [orderNumber, setOrderNumber] = useState('')
  const [orderDate, setOrderDate] = useState(today)
  const [supplierName, setSupplierName] = useState('')
  const [currencyCode, setCurrencyCode] = useState('USD')
  const [quoteId, setQuoteId] = useState('')
  const [quoteSearch, setQuoteSearch] = useState('')
  const [investmentNotes, setInvestmentNotes] = useState('')
  const [lines, setLines] = useState([createEmptyLine()])

  // Build a lookup: display label → id
  const quoteOptions = useMemo(
    () =>
      quotes.map((q) => ({
        id: q.id,
        label: `${q.number}${q.customerName || q.partyName ? ` — ${q.customerName || q.partyName}` : ''}`,
      })),
    [quotes],
  )

  // Populate form when entering edit mode or when a prefill quote is set
  useEffect(() => {
    if (initialPO) {
      const d = initialPO.date
        ? (typeof initialPO.date === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(initialPO.date)
            ? initialPO.date
            : new Date(initialPO.date).toISOString().split('T')[0])
        : today
      setOrderNumber(initialPO.number || '')
      setOrderDate(d)
      setSupplierName(initialPO.supplierName || initialPO.partyName || '')
      setCurrencyCode(initialPO.currencyCode || 'USD')
      setInvestmentNotes(initialPO.investmentNotes || '')
      const matchedOption = quoteOptions.find((opt) => opt.id === initialPO.quoteId)
      setQuoteId(initialPO.quoteId || '')
      setQuoteSearch(matchedOption?.label ?? '')
      setLines(
        Array.isArray(initialPO.lines) && initialPO.lines.length > 0
          ? initialPO.lines.map((l) => ({
              description: l.description ?? '',
              quantity: String(l.quantity ?? 1),
              unitPrice: String(l.unitPrice ?? 0),
              unitOfMeasure: l.unitOfMeasure || 'unit',
            }))
          : [createEmptyLine()],
      )
      return
    }

    // Pre-fill linked quote (from "Log expense" action on a quote card)
    if (prefillQuoteId) {
      const match = quoteOptions.find((opt) => opt.id === prefillQuoteId)
      if (match) {
        setQuoteId(match.id)
        setQuoteSearch(match.label)
      }
    }
  }, [initialPO, prefillQuoteId, quoteOptions, today])

  const handleQuoteSearchChange = (value) => {
    setQuoteSearch(value)
    const trimmed = value.trim()
    const match = quoteOptions.find(
      (opt) => opt.label.toLowerCase() === trimmed.toLowerCase(),
    )
    setQuoteId(match ? match.id : '')
  }

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

    const payload = isEditMode
      ? {
          supplierName,
          purchaseOrderDate: orderDate,
          currencyCode,
          quoteId: quoteId || null,
          investmentNotes: investmentNotes.trim() || null,
          lines: lines.map((line) => ({
            description: line.description,
            quantity: Number(line.quantity) || 0,
            unitPrice: Number(line.unitPrice) || 0,
            unitOfMeasure: line.unitOfMeasure,
          })),
        }
      : {
          purchaseOrderNumber: orderNumber,
          purchaseOrderDate: orderDate,
          supplierName,
          currencyCode,
          quoteId: quoteId || null,
          investmentNotes: investmentNotes.trim() || null,
          lines: lines.map((line) => ({
            description: line.description,
            quantity: Number(line.quantity) || 0,
            unitPrice: Number(line.unitPrice) || 0,
            unitOfMeasure: line.unitOfMeasure,
          })),
        }

    const wasSuccessful = await onSubmit(payload)

    if (wasSuccessful && !isEditMode) {
      setOrderNumber('')
      setOrderDate(today)
      setSupplierName('')
      setCurrencyCode('USD')
      setQuoteId('')
      setQuoteSearch('')
      setInvestmentNotes('')
      setLines([createEmptyLine()])
    }
  }

  return (
    <form className="document-form" onSubmit={handleSubmit}>
      {isEditMode && initialPO?.number ? (
        <div className="form-hint form-hint--warning">
          {t('purchaseOrderForm.editingLabel')}{' '}
          <span className="form-hint__strong">{initialPO.number}</span>
        </div>
      ) : null}
      <div className="field-grid">
        <label>
          {t('purchaseOrderForm.orderNumberLabel')}
          <input
            type="text"
            value={orderNumber}
            onChange={(event) => setOrderNumber(event.target.value)}
            required
            placeholder={t('purchaseOrderForm.orderNumberPlaceholder')}
          />
        </label>
        <label>
          {t('purchaseOrderForm.orderDateLabel')}
          <input
            type="date"
            value={orderDate}
            onChange={(event) => setOrderDate(event.target.value)}
            required
          />
        </label>
        <label>
          {t('purchaseOrderForm.supplierNameLabel')}
          <input
            type="text"
            value={supplierName}
            onChange={(event) => setSupplierName(event.target.value)}
            required
            placeholder={t('purchaseOrderForm.supplierNamePlaceholder')}
          />
        </label>
        <label>
          {t('purchaseOrderForm.currencyLabel')}
          <select value={currencyCode} onChange={(event) => setCurrencyCode(event.target.value)}>
            {CURRENCY_CODES.map((code) => (
              <option key={code} value={code}>
                {code}
              </option>
            ))}
          </select>
        </label>
        {quotes.length > 0 ? (
          <label>
            {t('purchaseOrderForm.linkedQuoteLabel')}
            <input
              type="text"
              value={quoteSearch}
              list={quoteListId}
              onChange={(event) => handleQuoteSearchChange(event.target.value)}
              placeholder={t('purchaseOrderForm.linkedQuotePlaceholder')}
              autoComplete="off"
            />
            <datalist id={quoteListId}>
              {quoteOptions.map((opt) => (
                <option key={opt.id} value={opt.label} />
              ))}
            </datalist>
            {quoteSearch && !quoteId ? (
              <p className="input-hint input-hint--warning">
                {t('purchaseOrderForm.linkedQuoteNoMatch')}
              </p>
            ) : null}
          </label>
        ) : null}
      </div>

      <div className="form-section">
        <label>
          {t('purchaseOrderForm.investmentNotesLabel')}
          <textarea
            rows="4"
            value={investmentNotes}
            onChange={(event) => setInvestmentNotes(event.target.value)}
            placeholder={t('purchaseOrderForm.investmentNotesPlaceholder')}
            maxLength={4000}
          />
        </label>
      </div>

      <div className="form-section">
        <div className="form-section__header">
          <h4>{t('purchaseOrderForm.lineItemsHeading')}</h4>
          <button
            type="button"
            className="button button--secondary"
            onClick={addLine}
          >
            {t('purchaseOrderForm.addLine')}
          </button>
        </div>
        {lines.map((line, index) => (
          <div key={index} className="line-row">
            <label className="line-row__description">
              {t('purchaseOrderForm.descriptionLabel')}
              <textarea
                rows="3"
                value={line.description}
                onChange={(event) =>
                  updateLine(index, 'description', event.target.value)
                }
                required
                placeholder={t('purchaseOrderForm.descriptionPlaceholder')}
              />
            </label>
            <label>
              {t('purchaseOrderForm.quantityLabel')}
              <input
                type="number"
                min="1"
                value={line.quantity}
                onChange={(event) => updateLine(index, 'quantity', event.target.value)}
                required
              />
            </label>
            <label>
              {t('purchaseOrderForm.unitOfMeasureLabel')}
              <select
                value={line.unitOfMeasure}
                onChange={(event) => updateLine(index, 'unitOfMeasure', event.target.value)}
                required
              >
                {unitOptions.map((code) => (
                  <option key={code} value={code}>
                    {t(`units.${code}`)}
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t('purchaseOrderForm.unitPriceLabel')}
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
              aria-label={t('purchaseOrderForm.removeLine')}
              disabled={lines.length === 1}
            >
              ×
            </button>
          </div>
        ))}
      </div>

      <div className="form-actions">
        {isEditMode && onCancelEdit ? (
          <button
            type="button"
            className="button button--ghost"
            onClick={onCancelEdit}
            disabled={isSubmitting}
          >
            {t('purchaseOrderForm.cancelEdit')}
          </button>
        ) : null}
        <button type="submit" className="button" disabled={isSubmitting}>
          {isSubmitting
            ? isEditMode
              ? t('purchaseOrderForm.updating')
              : t('purchaseOrderForm.submitting')
            : isEditMode
              ? t('purchaseOrderForm.updateSubmit')
              : t('purchaseOrderForm.submit')}
        </button>
      </div>
    </form>
  )
}

export default PurchaseOrderForm
