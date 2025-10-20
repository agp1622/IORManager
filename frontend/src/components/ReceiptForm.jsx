import { useState } from 'react'

const createEmptyPayment = () => ({
  method: '',
  amount: '0',
})

const ReceiptForm = ({ onSubmit, isSubmitting }) => {
  const today = new Date().toISOString().split('T')[0]
  const [receiptNumber, setReceiptNumber] = useState('')
  const [receiptDate, setReceiptDate] = useState(today)
  const [customerName, setCustomerName] = useState('')
  const [referenceNumber, setReferenceNumber] = useState('')
  const [payments, setPayments] = useState([createEmptyPayment()])

  const updatePayment = (index, field, value) => {
    setPayments((current) =>
      current.map((payment, paymentIndex) =>
        paymentIndex === index ? { ...payment, [field]: value } : payment,
      ),
    )
  }

  const addPayment = () => {
    setPayments((current) => [...current, createEmptyPayment()])
  }

  const removePayment = (index) => {
    setPayments((current) =>
      current.length > 1
        ? current.filter((_, paymentIndex) => paymentIndex !== index)
        : current,
    )
  }

  const handleSubmit = async (event) => {
    event.preventDefault()

    const payload = {
      receiptNumber,
      receiptDate,
      customerName,
      referenceNumber: referenceNumber || null,
      payments: payments.map((payment) => ({
        method: payment.method,
        amount: Number(payment.amount) || 0,
      })),
    }

    const wasSuccessful = await onSubmit(payload)

    if (wasSuccessful) {
      setReceiptNumber('')
      setReceiptDate(today)
      setCustomerName('')
      setReferenceNumber('')
      setPayments([createEmptyPayment()])
    }
  }

  return (
    <form className="document-form" onSubmit={handleSubmit}>
      <div className="field-grid">
        <label>
          Receipt number
          <input
            type="text"
            value={receiptNumber}
            onChange={(event) => setReceiptNumber(event.target.value)}
            required
            placeholder="RCPT-001"
          />
        </label>
        <label>
          Receipt date
          <input
            type="date"
            value={receiptDate}
            onChange={(event) => setReceiptDate(event.target.value)}
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
        <label>
          Reference number (optional)
          <input
            type="text"
            value={referenceNumber}
            onChange={(event) => setReferenceNumber(event.target.value)}
            placeholder="INV-001"
          />
        </label>
      </div>

      <div className="form-section">
        <div className="form-section__header">
          <h4>Payments</h4>
          <button
            type="button"
            className="button button--secondary"
            onClick={addPayment}
          >
            Add payment
          </button>
        </div>
        {payments.map((payment, index) => (
          <div key={index} className="line-row">
            <label>
              Method
              <input
                type="text"
                value={payment.method}
                onChange={(event) =>
                  updatePayment(index, 'method', event.target.value)
                }
                required
                placeholder="Bank transfer"
              />
            </label>
            <label>
              Amount
              <input
                type="number"
                min="0"
                step="0.01"
                value={payment.amount}
                onChange={(event) =>
                  updatePayment(index, 'amount', event.target.value)
                }
                required
              />
            </label>
            <button
              type="button"
              className="button button--icon"
              onClick={() => removePayment(index)}
              aria-label="Remove payment"
              disabled={payments.length === 1}
            >
              ×
            </button>
          </div>
        ))}
      </div>

      <button type="submit" className="button" disabled={isSubmitting}>
        {isSubmitting ? 'Saving receipt…' : 'Save receipt'}
      </button>
    </form>
  )
}

export default ReceiptForm
