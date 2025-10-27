import { useState } from 'react'

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

const PurchaseOrderForm = ({ onSubmit, isSubmitting, t = (value) => value }) => {
  const today = new Date().toISOString().split('T')[0]
  const [orderNumber, setOrderNumber] = useState('')
  const [orderDate, setOrderDate] = useState(today)
  const [supplierName, setSupplierName] = useState('')
  const [lines, setLines] = useState([createEmptyLine()])

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

    const payload = {
      orderNumber,
      orderDate,
      supplierName,
      lines: lines.map((line) => ({
        description: line.description,
        quantity: Number(line.quantity) || 0,
        unitPrice: Number(line.unitPrice) || 0,
        unitOfMeasure: line.unitOfMeasure,
      })),
    }

    const wasSuccessful = await onSubmit(payload)

    if (wasSuccessful) {
      setOrderNumber('')
      setOrderDate(today)
      setSupplierName('')
      setLines([createEmptyLine()])
    }
  }

  return (
    <form className="document-form" onSubmit={handleSubmit}>
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

      <button type="submit" className="button" disabled={isSubmitting}>
        {isSubmitting ? t('purchaseOrderForm.submitting') : t('purchaseOrderForm.submit')}
      </button>
    </form>
  )
}

export default PurchaseOrderForm
