import { useState } from 'react'

const createEmptyLine = () => ({
  description: '',
  quantity: '1',
  unitPrice: '0',
})

const InvoiceForm = ({ onSubmit, isSubmitting }) => {
  const today = new Date().toISOString().split('T')[0]
  const [invoiceNumber, setInvoiceNumber] = useState('')
  const [invoiceDate, setInvoiceDate] = useState(today)
  const [customerName, setCustomerName] = useState('')
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
      invoiceNumber,
      invoiceDate,
      customerName,
      lines: lines.map((line) => ({
        description: line.description,
        quantity: Number(line.quantity) || 0,
        unitPrice: Number(line.unitPrice) || 0,
      })),
    }

    const wasSuccessful = await onSubmit(payload)

    if (wasSuccessful) {
      setInvoiceNumber('')
      setInvoiceDate(today)
      setCustomerName('')
      setLines([createEmptyLine()])
    }
  }

  return (
    <form className="document-form" onSubmit={handleSubmit}>
      <div className="field-grid">
        <label>
          Invoice number
          <input
            type="text"
            value={invoiceNumber}
            onChange={(event) => setInvoiceNumber(event.target.value)}
            required
            placeholder="INV-001"
          />
        </label>
        <label>
          Invoice date
          <input
            type="date"
            value={invoiceDate}
            onChange={(event) => setInvoiceDate(event.target.value)}
            required
          />
        </label>
        <label>
          Customer name
          <input
            type="text"
            value={customerName}
            onChange={(event) => setCustomerName(event.target.value)}
            required
            placeholder="Acme Corp"
          />
        </label>
      </div>

      <div className="form-section">
        <div className="form-section__header">
          <h4>Line items</h4>
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
                placeholder="Consulting services"
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
        {isSubmitting ? 'Saving invoice…' : 'Save invoice'}
      </button>
    </form>
  )
}

export default InvoiceForm
