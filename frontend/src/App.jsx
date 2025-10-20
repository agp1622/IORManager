import { useCallback, useEffect, useMemo, useState } from 'react'
import InvoiceForm from './components/InvoiceForm'
import PurchaseOrderForm from './components/PurchaseOrderForm'
import ReceiptForm from './components/ReceiptForm'
import './App.css'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5031/api'

const SECTION_CONFIG = {
  invoices: {
    title: 'Invoices',
    description: 'Review customer invoices and capture new billable work.',
    endpoint: 'Invoices',
    singular: 'invoice',
    partyLabel: 'Customer',
  },
  purchaseOrders: {
    title: 'Purchase orders',
    description: 'Track supplier commitments and the items you have ordered.',
    endpoint: 'PurchaseOrders',
    singular: 'purchase order',
    partyLabel: 'Supplier',
  },
  receipts: {
    title: 'Receipts',
    description: 'Monitor customer payments and reconcile outstanding balances.',
    endpoint: 'Receipts',
    singular: 'receipt',
    partyLabel: 'Customer',
  },
}

const currencyFormatter = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
})

const formatCurrency = (value) => currencyFormatter.format(value)

const formatDate = (value) => {
  if (!value) {
    return '—'
  }

  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) {
    return value
  }

  return parsed.toLocaleDateString()
}

const DocumentCard = ({ document, sectionKey }) => {
  const config = SECTION_CONFIG[sectionKey]
  const lines = document.lines ?? []
  const payments = document.payments ?? []

  return (
    <article className="document-card">
      <header className="document-card__header">
        <div>
          <h3>{document.number}</h3>
          <p className="document-card__date">{formatDate(document.date)}</p>
        </div>
        <p className="document-card__total">{formatCurrency(document.totalAmount)}</p>
      </header>
      <dl className="document-card__summary">
        <div>
          <dt>{config.partyLabel}</dt>
          <dd>{document.partyName}</dd>
        </div>
        {document.referenceNumber && (
          <div>
            <dt>Reference</dt>
            <dd>{document.referenceNumber}</dd>
          </div>
        )}
        <div>
          <dt>Identifier</dt>
          <dd>{document.id}</dd>
        </div>
      </dl>

      {lines.length > 0 && (
        <div className="document-card__table">
          <h4>Line items</h4>
          <table>
            <thead>
              <tr>
                <th scope="col">Description</th>
                <th scope="col">Quantity</th>
                <th scope="col">Unit price</th>
                <th scope="col">Line total</th>
              </tr>
            </thead>
            <tbody>
              {lines.map((line, index) => (
                <tr key={`${document.id}-line-${index}`}>
                  <td>{line.description}</td>
                  <td>{line.quantity}</td>
                  <td>{formatCurrency(line.unitPrice)}</td>
                  <td>{formatCurrency(line.lineTotal)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {payments.length > 0 && (
        <div className="document-card__table">
          <h4>Payments</h4>
          <table>
            <thead>
              <tr>
                <th scope="col">Method</th>
                <th scope="col">Amount</th>
              </tr>
            </thead>
            <tbody>
              {payments.map((payment, index) => (
                <tr key={`${document.id}-payment-${index}`}>
                  <td>{payment.method}</td>
                  <td>{formatCurrency(payment.amount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </article>
  )
}

function App() {
  const [activeSection, setActiveSection] = useState('invoices')
  const [documents, setDocuments] = useState({
    invoices: [],
    purchaseOrders: [],
    receipts: [],
  })
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [status, setStatus] = useState(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const loadSection = useCallback(async (sectionKey) => {
    const config = SECTION_CONFIG[sectionKey]
    if (!config) {
      return
    }

    setLoading(true)
    setError(null)

    try {
      const response = await fetch(`${API_BASE_URL}/${config.endpoint}`)
      if (!response.ok) {
        throw new Error(`Unable to load ${config.title.toLowerCase()}.`)
      }

      const data = await response.json()
      setDocuments((current) => ({ ...current, [sectionKey]: data }))
    } catch (requestError) {
      setError(requestError.message)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    loadSection(activeSection)
  }, [activeSection, loadSection])

  useEffect(() => {
    if (!status) {
      return undefined
    }

    const timeoutId = window.setTimeout(() => setStatus(null), 5000)
    return () => window.clearTimeout(timeoutId)
  }, [status])

  const handleCreate = useCallback(
    async (sectionKey, payload) => {
      const config = SECTION_CONFIG[sectionKey]
      if (!config) {
        return false
      }

      setIsSubmitting(true)
      setStatus(null)

      try {
        const response = await fetch(`${API_BASE_URL}/${config.endpoint}`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify(payload),
        })

        if (!response.ok) {
          const errorBody = await response.json().catch(() => null)
          const problemDetail = errorBody?.title || errorBody?.detail
          throw new Error(
            problemDetail || `Unable to save the ${config.singular}.`,
          )
        }

        setStatus({
          type: 'success',
          message: `The ${config.singular} was created successfully.`,
        })
        await loadSection(sectionKey)
        return true
      } catch (requestError) {
        setStatus({ type: 'error', message: requestError.message })
        return false
      } finally {
        setIsSubmitting(false)
      }
    },
    [loadSection],
  )

  const currentDocuments = documents[activeSection] ?? []
  const config = SECTION_CONFIG[activeSection]

  const FormComponent = useMemo(() => {
    switch (activeSection) {
      case 'purchaseOrders':
        return PurchaseOrderForm
      case 'receipts':
        return ReceiptForm
      case 'invoices':
      default:
        return InvoiceForm
    }
  }, [activeSection])

  return (
    <div className="layout">
      <header className="page-header">
        <h1>IOR Manager</h1>
        <p className="page-header__subtitle">
          A lightweight dashboard for monitoring invoices, purchase orders and
          receipts.
        </p>
      </header>

      <nav className="section-tabs" aria-label="Financial document sections">
        {Object.entries(SECTION_CONFIG).map(([key, value]) => (
          <button
            key={key}
            type="button"
            className={`section-tab ${key === activeSection ? 'is-active' : ''}`}
            onClick={() => setActiveSection(key)}
          >
            {value.title}
          </button>
        ))}
      </nav>

      <section className="section-content">
        <div className="section-intro">
          <h2>{config.title}</h2>
          <p>{config.description}</p>
        </div>

        {status && (
          <div className={`banner banner--${status.type}`} role="status">
            {status.message}
          </div>
        )}

        {loading ? (
          <p className="muted">Loading {config.title.toLowerCase()}…</p>
        ) : error ? (
          <div className="banner banner--error" role="alert">
            {error}
          </div>
        ) : currentDocuments.length === 0 ? (
          <p className="muted">No {config.title.toLowerCase()} yet.</p>
        ) : (
          <div className="document-grid">
            {currentDocuments.map((document) => (
              <DocumentCard
                key={document.id}
                document={document}
                sectionKey={activeSection}
              />
            ))}
          </div>
        )}
      </section>

      <section className="section-content">
        <div className="section-intro">
          <h2>Create a new {config.singular}</h2>
          <p>
            Capture the essential details and the dashboard will refresh as soon
            as the API confirms the new {config.singular}.
          </p>
        </div>

        <FormComponent
          onSubmit={(payload) => handleCreate(activeSection, payload)}
          isSubmitting={isSubmitting}
        />
      </section>
    </div>
  )
}

export default App
