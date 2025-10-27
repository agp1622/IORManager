import { useCallback, useEffect, useMemo, useState } from 'react'
import InvoiceForm from './components/InvoiceForm'
import PurchaseOrderForm from './components/PurchaseOrderForm'
import ReceiptForm from './components/ReceiptForm'
import './App.css'
import { createTranslator, LANGUAGES } from './i18n'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5031/api'
const CURRENCY_CODES = ['USD', 'DOP']

const createSectionConfig = (t) => ({
  invoices: {
    title: t('sections.invoices.title'),
    description: t('sections.invoices.description'),
    endpoint: 'Invoices',
    singular: t('sections.invoices.singular'),
    partyLabel: t('sections.invoices.partyLabel'),
    loading: t('sections.invoices.loading'),
    empty: t('sections.invoices.empty'),
    loadError: t('sections.invoices.loadError'),
    createHeading: t('sections.invoices.createHeading'),
    createDescription: t('sections.invoices.createDescription'),
    createSuccess: t('sections.invoices.createSuccess'),
    createError: t('sections.invoices.createError'),
  },
  purchaseOrders: {
    title: t('sections.purchaseOrders.title'),
    description: t('sections.purchaseOrders.description'),
    endpoint: 'PurchaseOrders',
    singular: t('sections.purchaseOrders.singular'),
    partyLabel: t('sections.purchaseOrders.partyLabel'),
    loading: t('sections.purchaseOrders.loading'),
    empty: t('sections.purchaseOrders.empty'),
    loadError: t('sections.purchaseOrders.loadError'),
    createHeading: t('sections.purchaseOrders.createHeading'),
    createDescription: t('sections.purchaseOrders.createDescription'),
    createSuccess: t('sections.purchaseOrders.createSuccess'),
    createError: t('sections.purchaseOrders.createError'),
  },
  receipts: {
    title: t('sections.receipts.title'),
    description: t('sections.receipts.description'),
    endpoint: 'Receipts',
    singular: t('sections.receipts.singular'),
    partyLabel: t('sections.receipts.partyLabel'),
    loading: t('sections.receipts.loading'),
    empty: t('sections.receipts.empty'),
    loadError: t('sections.receipts.loadError'),
    createHeading: t('sections.receipts.createHeading'),
    createDescription: t('sections.receipts.createDescription'),
    createSuccess: t('sections.receipts.createSuccess'),
    createError: t('sections.receipts.createError'),
  },
})

const DocumentCard = ({ document, config, t, formatCurrency, formatDate }) => {
  const lines = document.lines ?? []
  const payments = document.payments ?? []
  const currencyCode = document.currencyCode
  const cultureName = document.cultureName
  const partyName =
    document.partyName ||
    document.customerName ||
    document.supplierName ||
    '—'

  return (
    <article className="document-card">
      <header className="document-card__header">
        <div>
          <h3>{document.number}</h3>
          <p className="document-card__date">{formatDate(document.date, cultureName)}</p>
        </div>
        <p className="document-card__total">
          {formatCurrency(document.totalAmount, currencyCode, cultureName)}
        </p>
      </header>
      <dl className="document-card__summary">
        <div>
          <dt>{config.partyLabel}</dt>
          <dd>{partyName}</dd>
        </div>
        {document.referenceNumber && (
          <div>
            <dt>{t('documentCard.referenceLabel')}</dt>
            <dd>{document.referenceNumber}</dd>
          </div>
        )}
        <div>
          <dt>{t('documentCard.identifierLabel')}</dt>
          <dd>{document.id}</dd>
        </div>
      </dl>

      {lines.length > 0 && (
        <div className="document-card__table">
          <h4>{t('documentCard.lineItemsHeading')}</h4>
          <table>
            <thead>
              <tr>
                <th scope="col">{t('documentCard.descriptionLabel')}</th>
                <th scope="col">{t('documentCard.quantityLabel')}</th>
                <th scope="col">{t('documentCard.unitPriceLabel')}</th>
                <th scope="col">{t('documentCard.lineTotalLabel')}</th>
              </tr>
            </thead>
            <tbody>
              {lines.map((line, index) => (
                <tr key={`${document.id}-line-${index}`}>
                  <td>{line.description}</td>
                  <td>{line.quantity}</td>
                  <td>{formatCurrency(line.unitPrice, currencyCode, cultureName)}</td>
                  <td>{formatCurrency(line.lineTotal, currencyCode, cultureName)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {payments.length > 0 && (
        <div className="document-card__table">
          <h4>{t('documentCard.paymentsHeading')}</h4>
          <table>
            <thead>
              <tr>
                <th scope="col">{t('documentCard.paymentMethodLabel')}</th>
                <th scope="col">{t('documentCard.paymentAmountLabel')}</th>
              </tr>
            </thead>
            <tbody>
              {payments.map((payment, index) => (
                <tr key={`${document.id}-payment-${index}`}>
                  <td>{payment.method}</td>
                  <td>{formatCurrency(payment.amount, currencyCode, cultureName)}</td>
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
  const [language, setLanguage] = useState('en')
  const [defaultCurrency, setDefaultCurrency] = useState('USD')
  const translate = useMemo(() => createTranslator(language), [language])
  const locale = useMemo(
    () => LANGUAGES.find((entry) => entry.value === language)?.locale ?? 'en-US',
    [language],
  )
  const sectionConfig = useMemo(() => createSectionConfig(translate), [translate])

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

  const formatCurrency = useCallback(
    (value, currencyCode, cultureName) => {
      if (value === undefined || value === null) {
        return value ?? '—'
      }

      const numericValue = Number(value)
      if (Number.isNaN(numericValue)) {
        return value
      }

      const resolvedCurrency = currencyCode || defaultCurrency
      const resolvedLocale = cultureName || locale

      try {
        const formatter = new Intl.NumberFormat(resolvedLocale, {
          style: 'currency',
          currency: resolvedCurrency,
        })
        return formatter.format(numericValue)
      } catch (error) {
        return `${resolvedCurrency} ${numericValue.toFixed(2)}`
      }
    },
    [defaultCurrency, locale],
  )

  const formatDate = useCallback(
    (value, cultureName) => {
      if (!value) {
        return '—'
      }

      const parsed = new Date(value)
      if (Number.isNaN(parsed.getTime())) {
        return value
      }

      const resolvedLocale = cultureName || locale
      try {
        return parsed.toLocaleDateString(resolvedLocale)
      } catch (error) {
        return parsed.toLocaleDateString()
      }
    },
    [locale],
  )

  const loadSection = useCallback(
    async (sectionKey) => {
      const config = sectionConfig[sectionKey]
      if (!config) {
        return
      }

      setLoading(true)
      setError(null)

      try {
        const response = await fetch(`${API_BASE_URL}/${config.endpoint}`)
        if (!response.ok) {
          throw new Error(config.loadError)
        }

        const data = await response.json()
        setDocuments((current) => ({ ...current, [sectionKey]: data }))
      } catch (requestError) {
        setError(requestError.message || config.loadError)
      } finally {
        setLoading(false)
      }
    },
    [sectionConfig],
  )

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

  const triggerPdfDownload = useCallback((fileName, base64Content) => {
    if (!base64Content) {
      throw new Error('Missing PDF data.')
    }

    const link = document.createElement('a')
    link.href = `data:application/pdf;base64,${base64Content}`
    link.download = fileName || 'invoice.pdf'
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
  }, [])

  const handleCreate = useCallback(
    async (sectionKey, payload) => {
      const config = sectionConfig[sectionKey]
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

        const responseBody = await response.json().catch(() => null)

        if (!response.ok) {
          const problemDetail = responseBody?.title || responseBody?.detail
          throw new Error(problemDetail || config.createError)
        }

        let downloadSucceeded = true
        if (sectionKey === 'invoices' && responseBody?.pdfBase64) {
          try {
            triggerPdfDownload(responseBody.pdfFileName, responseBody.pdfBase64)
          } catch (downloadError) {
            downloadSucceeded = false
            setStatus({ type: 'error', message: translate('pdf.downloadError') })
          }
        }

        if (downloadSucceeded) {
          setStatus({ type: 'success', message: config.createSuccess })
        }

        await loadSection(sectionKey)
        return true
      } catch (requestError) {
        setStatus({
          type: 'error',
          message: requestError.message || sectionConfig[sectionKey].createError,
        })
        return false
      } finally {
        setIsSubmitting(false)
      }
    },
    [loadSection, sectionConfig, translate, triggerPdfDownload],
  )

  const currentDocuments = documents[activeSection] ?? []
  const config = sectionConfig[activeSection]

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
        <h1>{translate('app.title')}</h1>
        <p className="page-header__subtitle">{translate('app.subtitle')}</p>
      </header>

      <div className="toolbar">
        <label className="toolbar__control">
          {translate('controls.language')}
          <select value={language} onChange={(event) => setLanguage(event.target.value)}>
            {LANGUAGES.map((entry) => (
              <option key={entry.value} value={entry.value}>
                {entry.label}
              </option>
            ))}
          </select>
        </label>
        <label className="toolbar__control">
          {translate('controls.currency')}
          <select
            value={defaultCurrency}
            onChange={(event) => setDefaultCurrency(event.target.value)}
          >
            {CURRENCY_CODES.map((code) => (
              <option key={code} value={code}>
                {translate(`currencies.${code}`)}
              </option>
            ))}
          </select>
        </label>
      </div>

      <nav className="section-tabs" aria-label="Financial document sections">
        {Object.entries(sectionConfig).map(([key, value]) => (
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
          <p className="muted">{config.loading}</p>
        ) : error ? (
          <div className="banner banner--error" role="alert">
            {error}
          </div>
        ) : currentDocuments.length === 0 ? (
          <p className="muted">{config.empty}</p>
        ) : (
          <div className="document-grid">
            {currentDocuments.map((document) => (
              <DocumentCard
                key={document.id}
                document={document}
                config={config}
                t={translate}
                formatCurrency={formatCurrency}
                formatDate={formatDate}
              />
            ))}
          </div>
        )}
      </section>

      <section className="section-content">
        <div className="section-intro">
          <h2>{config.createHeading}</h2>
          <p>{config.createDescription}</p>
        </div>

        <FormComponent
          onSubmit={(payload) => handleCreate(activeSection, payload)}
          isSubmitting={isSubmitting}
          t={translate}
          {...(activeSection === 'invoices' ? { defaultCurrency, locale } : {})}
        />
      </section>
    </div>
  )
}

export default App
