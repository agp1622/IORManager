import { useState } from 'react'

const createEmptyLine = () => ({
  description: '',
  quantity: '1',
  unitPrice: '0',
})

const PurchaseOrderForm = ({ onSubmit, isSubmitting }) => {
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
      orderNumber,
      orderDate,
      supplierName,
      lines: lines.map((line) => ({
        description: line.description,
        quantity: Number(line.quantity) || 0,
        unitPrice: Number(line.unitPrice) || 0,
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
          Order number
          <input
            type="text"
            value={orderNumber}
            onChange={(event) => setOrderNumber(event.target.value)}
            required
            placeholder="PO-001"
          />
        </label>
        <label>
          Order date
          <input
            type="date"
            value={orderDate}
            onChange={(event) => setOrderDate(event.target.value)}
            required
          />
        </label>
        <label>
          Supplier name
          <input
            type="text"
            value={supplierName}
            onChange={(event) => setSupplierName(event.target.value)}
            required
            placeholder="Supply Co"
          />
        </label>
      </div>

      <div className="form-section">
        <div className="form-section__header">
          <h4>Order lines</h4>
          <button
            type="button"
            className="button button--secondary"
            onClick={addLine}
          >
            Add line
          </button>
        </div>
        {lines.map((line, index) => (
          <div key={index} className="line-row">
            <label>
              Description
              <input
                type="text"
                value={line.description}
                onChange={(event) =>
                  updateLine(index, 'description', event.target.value)
                }
                required
                placeholder="Laptops"
              />
            </label>
            <label>
              Quantity
              <input
                type="number"
                min="1"
                value={line.quantity}
                onChange={(event) => updateLine(index, 'quantity', event.target.value)}
                required
              />
            </label>
            <label>
              Unit price
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
              aria-label="Remove line item"
              disabled={lines.length === 1}
            >
              ×
            </button>
          </div>
        ))}
      </div>

      <button type="submit" className="button" disabled={isSubmitting}>
        {isSubmitting ? 'Saving purchase order…' : 'Save purchase order'}
      </button>
    </form>
  )
}

export default PurchaseOrderForm
