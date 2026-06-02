import { useEffect, useId, useMemo, useState } from 'react'

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
  customers = [],
}) => {
  const today = new Date().toISOString().split('T')[0]
  const formInstanceId = useId()
  const [invoiceDate, setInvoiceDate] = useState(today)
  const [customerName, setCustomerName] = useState('')
  const [customerAddress, setCustomerAddress] = useState('')
  const [customerContact, setCustomerContact] = useState('')
  const [currencyCode, setCurrencyCode] = useState(defaultCurrency)
  const [customerPONumber, setCustomerPONumber] = useState('')
  const [itbisRate, setItbisRate] = useState('')
  const [comments, setComments] = useState('')
  const [lines, setLines] = useState([createEmptyLine()])
  const isEditMode = mode === 'edit' && initialInvoice
  const customerListId = `${formInstanceId}-customers`

  const savedCustomers = useMemo(
    () =>
      (Array.isArray(customers) ? customers : [])
        .map((customer) => ({
          name: (customer?.name || '').trim(),
          normalizedName: (customer?.name || '').trim().toLowerCase(),
          address: (customer?.address || '').trim(),
          contact: (customer?.contact || '').trim(),
        }))
        .filter((customer) => customer.name.length > 0),
    [customers],
  )

  const normalizeDateOnly = (value, fallback) => {
    if (!value) {
      return fallback
    }

    if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value)) {
      return value
    }

    const parsed = new Date(value)
    if (Number.isNaN(parsed.getTime())) {
      return fallback
    }

    return parsed.toISOString().split('T')[0]
  }

  useEffect(() => {
    if (initialInvoice) {
      const normalizedDate = normalizeDateOnly(initialInvoice.date, today)
      setInvoiceDate(normalizedDate)
      setCustomerName(initialInvoice.customerName || initialInvoice.partyName || '')
      setCustomerAddress(initialInvoice.customerAddress || '')
      setCustomerContact(initialInvoice.customerContact || '')
      setCurrencyCode(initialInvoice.currencyCode || defaultCurrency)
      setCustomerPONumber(initialInvoice.customerPONumber || '')
      setComments(initialInvoice.comments || '')
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
    setCustomerAddress('')
    setCustomerContact('')
    setCurrencyCode(defaultCurrency)
    setCustomerPONumber('')
    setComments('')
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

  const applySavedCustomer = (rawName) => {
    const normalizedName = rawName.trim().toLowerCase()
    if (!normalizedName) {
      return
    }

    const matchedCustomer = savedCustomers.find(
      (customer) => customer.normalizedName === normalizedName,
    )
    if (!matchedCustomer) {
      return
    }

    setCustomerAddress(matchedCustomer.address)
    setCustomerContact(matchedCustomer.contact)
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

    const normalizedAddress = customerAddress.trim()
    const normalizedContact = customerContact.trim()

    const payload = {
      invoiceDate,
      customerName: customerName.trim(),
      customerAddress: normalizedAddress,
      customerContact: normalizedContact,
      currencyCode,
      customerPONumber: customerPONumber.trim() || null,
      comments: comments.trim() || null,
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
      setCustomerAddress('')
      setCustomerContact('')
      setCurrencyCode(defaultCurrency)
      setCustomerPONumber('')
      setComments('')
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
            disabled
            readOnly
          />
          <p className="input-hint">{t('invoiceForm.quoteDateFixedHint')}</p>
        </label>
        <label>
          {t('invoiceForm.customerNameLabel')}
          <input
            type="text"
            value={customerName}
            list={customerListId}
            onChange={(event) => {
              const nextValue = event.target.value
              setCustomerName(nextValue)
              applySavedCustomer(nextValue)
            }}
            required
            placeholder={t('invoiceForm.customerNamePlaceholder')}
          />
          <datalist id={customerListId}>
            {savedCustomers.map((customer) => (
              <option key={customer.name} value={customer.name} />
            ))}
          </datalist>
          <p className="input-hint">{t('invoiceForm.customerSavedHint')}</p>
        </label>
        <label>
          {t('invoiceForm.customerAddressLabel')}
          <textarea
            value={customerAddress}
            onChange={(event) => setCustomerAddress(event.target.value)}
            placeholder={t('invoiceForm.customerAddressPlaceholder')}
            rows="2"
            required
          />
        </label>
        <label>
          {t('invoiceForm.customerContactLabel')}
          <input
            type="text"
            value={customerContact}
            onChange={(event) => setCustomerContact(event.target.value)}
            placeholder={t('invoiceForm.customerContactPlaceholder')}
            required
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
        <label>
          {t('quoteAttachments.customerPONumberLabel')}
          <input
            type="text"
            value={customerPONumber}
            onChange={(event) => setCustomerPONumber(event.target.value)}
            placeholder={t('quoteAttachments.customerPONumberPlaceholder')}
            maxLength={100}
          />
          <p className="input-hint">{t('quoteAttachments.customerPONumberHint')}</p>
        </label>
        <label className="field-grid__full">
          {t('invoiceForm.commentsLabel')}
          <textarea
            value={comments}
            onChange={(event) => setComments(event.target.value)}
            placeholder={t('invoiceForm.commentsPlaceholder')}
            rows="4"
            maxLength={2000}
          />
          <p className="input-hint">{t('invoiceForm.commentsHint')}</p>
        </label>
      </div>

      <div className="form-section">
        <div className="form-section__header">
          <h4>{t('invoiceForm.lineItemsHeading')}</h4>
          <button
            type="button"
            className="button button--icon button--icon-add-line"
            onClick={addLine}
            aria-label={t('invoiceForm.addLine')}
            title={t('invoiceForm.addLine')}
          >
            <span aria-hidden="true">+</span>
            <span className="sr-only">{t('invoiceForm.addLine')}</span>
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
