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

const DocumentList = ({
  documents,
  config,
  t,
  formatCurrency,
  formatDate,
  onDownloadPdf,
  downloadingId,
}) => {
  return (
    <div className="document-table-wrapper">
      <table className="document-table">
        <thead>
          <tr>
            <th scope="col">{t('documentList.number')}</th>
            <th scope="col">{t('documentList.date')}</th>
            <th scope="col">{t('documentList.party')}</th>
            <th scope="col">{t('documentList.total')}</th>
            <th scope="col">{t('documentList.currency')}</th>
            <th scope="col">{t('documentList.actions')}</th>
          </tr>
        </thead>
        <tbody>
          {documents.map((document) => {
            const currencyCode = document.currencyCode
            const cultureName = document.cultureName
            const partyName =
              document.partyName || document.customerName || document.supplierName || '—'
            const isDownloading = downloadingId === document.id

            return (
              <tr key={document.id}>
                <td data-heading={t('documentList.number')}>{document.number}</td>
                <td data-heading={t('documentList.date')}>
                  {formatDate(document.date, cultureName)}
                </td>
                <td data-heading={config.partyLabel}>{partyName}</td>
                <td data-heading={t('documentList.total')}>
                  {formatCurrency(document.totalAmount, currencyCode, cultureName)}
                </td>
                <td data-heading={t('documentList.currency')}>
                  {currencyCode || '—'}
                </td>
                <td
                  data-heading={t('documentList.actions')}
                  className="document-table__actions"
                >
                  <button
                    type="button"
                    className="button button--secondary"
                    onClick={() => onDownloadPdf(document)}
                    disabled={isDownloading}
                  >
                    {isDownloading
                      ? t('documentList.downloading')
                      : t('documentList.downloadPdf')}
                  </button>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}

function App() {
  const [language, setLanguage] = useState('es')
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
  const [filters, setFilters] = useState({ search: '', client: '', number: '', date: '' })
  const [downloadingId, setDownloadingId] = useState(null)

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
    setFilters({ search: '', client: '', number: '', date: '' })
  }, [activeSection])

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

  const filteredDocuments = useMemo(() => {
    const source = documents[activeSection] ?? []
    const searchTerm = filters.search.trim().toLowerCase()
    const clientTerm = filters.client.trim().toLowerCase()
    const numberTerm = filters.number.trim().toLowerCase()
    const dateTerm = filters.date

    const normalizeDate = (value) => {
      if (!value) {
        return ''
      }

      const parsed = new Date(value)
      if (Number.isNaN(parsed.getTime())) {
        return String(value).slice(0, 10)
      }

      return parsed.toISOString().slice(0, 10)
    }

    if (!searchTerm && !clientTerm && !numberTerm && !dateTerm) {
      return source
    }

    return source.filter((document) => {
      const partyName =
        document.partyName || document.customerName || document.supplierName || ''
      const partyLower = partyName.toLowerCase()
      const numberLower = (document.number || '').toLowerCase()
      const normalizedDate = normalizeDate(document.date)

      const matchesClient = !clientTerm || partyLower.includes(clientTerm)
      const matchesNumber = !numberTerm || numberLower.includes(numberTerm)
      const matchesDate = !dateTerm || normalizedDate === dateTerm

      let matchesSearch = true
      if (searchTerm) {
        const searchable = [
          numberLower,
          partyLower,
          (document.referenceNumber || '').toLowerCase(),
          (document.currencyCode || '').toLowerCase(),
          (document.id || '').toLowerCase(),
        ]

        if (Array.isArray(document.lines)) {
          document.lines.forEach((line) => {
            searchable.push((line.description || '').toLowerCase())
            searchable.push(String(line.quantity ?? '').toLowerCase())
            searchable.push((line.unitOfMeasure || '').toLowerCase())
          })
        }

        if (Array.isArray(document.payments)) {
          document.payments.forEach((payment) => {
            searchable.push((payment.method || '').toLowerCase())
            searchable.push(String(payment.amount ?? '').toLowerCase())
          })
        }

        matchesSearch = searchable.some((value) => value && value.includes(searchTerm))
      }

      return matchesClient && matchesNumber && matchesDate && matchesSearch
    })
  }, [documents, activeSection, filters])

  const currentDocuments = filteredDocuments
  const config = sectionConfig[activeSection]

  const handleDownloadFromList = useCallback(
    async (doc) => {
      const section = sectionConfig[activeSection]
      if (!section) {
        return
      }

      try {
        setDownloadingId(doc.id)
        const response = await fetch(
          `${API_BASE_URL}/${section.endpoint}/${doc.id}/pdf`,
        )

        if (!response.ok) {
          throw new Error(translate('pdf.downloadError'))
        }

        const blob = await response.blob()
        const downloadUrl = window.URL.createObjectURL(blob)
        const link = window.document.createElement('a')
        const safeNumber = doc.number?.replace?.(/\s+/g, '-') ?? doc.id
        link.href = downloadUrl
        link.download = `${section.singular}-${safeNumber}.pdf`
        window.document.body.appendChild(link)
        link.click()
        window.document.body.removeChild(link)
        window.URL.revokeObjectURL(downloadUrl)
      } catch (downloadError) {
        setStatus({
          type: 'error',
          message: downloadError.message || translate('pdf.downloadError'),
        })
      } finally {
        setDownloadingId(null)
      }
    },
    [activeSection, sectionConfig, translate],
  )

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

        <div className="filter-bar" role="search">
          <div className="filter-bar__grid">
            <label>
              {translate('filters.searchLabel')}
              <input
                type="search"
                value={filters.search}
                onChange={(event) =>
                  setFilters((current) => ({ ...current, search: event.target.value }))
                }
                placeholder={translate('filters.searchPlaceholder')}
              />
            </label>
            <label>
              {config.partyLabel}
              <input
                type="text"
                value={filters.client}
                onChange={(event) =>
                  setFilters((current) => ({ ...current, client: event.target.value }))
                }
                placeholder={config.partyLabel}
              />
            </label>
            <label>
              {activeSection === 'invoices'
                ? translate('filters.numberLabel')
                : translate('filters.genericNumberLabel')}
              <input
                type="text"
                value={filters.number}
                onChange={(event) =>
                  setFilters((current) => ({ ...current, number: event.target.value }))
                }
              />
            </label>
            <label>
              {translate('filters.dateLabel')}
              <input
                type="date"
                value={filters.date}
                onChange={(event) =>
                  setFilters((current) => ({ ...current, date: event.target.value }))
                }
              />
            </label>
          </div>
          <button
            type="button"
            className="button button--secondary"
            onClick={() => setFilters({ search: '', client: '', number: '', date: '' })}
          >
            {translate('filters.clear')}
          </button>
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
          <DocumentList
            documents={currentDocuments}
            config={config}
            t={translate}
            formatCurrency={formatCurrency}
            formatDate={formatDate}
            onDownloadPdf={handleDownloadFromList}
            downloadingId={downloadingId}
          />
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
