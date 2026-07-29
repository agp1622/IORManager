import { useEffect, useId, useMemo, useState } from 'react'
import ThemedSelect from './ThemedSelect'

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
      return
    }

    // Pre-fill linked quote (from "Create order" action on a quote card)
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

  const handleSubmit = async (event) => {
    event.preventDefault()

    const payload = isEditMode
      ? {
          supplierName,
          purchaseOrderDate: orderDate,
          currencyCode,
          quoteId: quoteId || null,
          investmentNotes: investmentNotes.trim() || null,
          lines: [],
        }
      : {
          purchaseOrderNumber: orderNumber,
          purchaseOrderDate: orderDate,
          supplierName,
          currencyCode,
          quoteId: quoteId || null,
          investmentNotes: investmentNotes.trim() || null,
          lines: [],
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
          <ThemedSelect
            value={currencyCode}
            onChange={setCurrencyCode}
            ariaLabel={t('purchaseOrderForm.currencyLabel')}
            options={CURRENCY_CODES.map((code) => ({ value: code, label: code }))}
          />
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

      {!isEditMode ? (
        <p className="form-hint">{t('purchaseOrderForm.expensesAfterCreateHint')}</p>
      ) : null}

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
