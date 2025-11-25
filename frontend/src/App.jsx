import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import InvoiceForm from './components/InvoiceForm'
import ReceiptForm from './components/ReceiptForm'
import './App.css'
import { createTranslator, LANGUAGES } from './i18n'
import PapavelagLogo from './assets/papavelag-logo.svg'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5031/api'
const CURRENCY_CODES = ['USD', 'DOP']
const ROWS_PER_PAGE = 10

const BRAND_NOTES = {
  quotes: 'Cotizaciones impactantes listas para convertirse en facturas.',
  receipts: 'Recibos elegantes que transmiten confianza y precisión.',
  default: 'Documentos oficiales con el sello PAPAVELAG.',
}

const getInitialTheme = () => {
  if (typeof window === 'undefined') {
    return 'light'
  }
  const stored = window.localStorage.getItem('ior-theme')
  if (stored === 'light' || stored === 'dark') {
    return stored
  }
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

const BrandPanel = ({ note, variant = 'default' }) => (
  <div className={`brand-panel ${variant === 'compact' ? 'brand-panel--compact' : ''}`}>
    <img src={PapavelagLogo} alt="Papavelag Technologies logo" className="brand-panel__logo" />
    <div className="brand-panel__text">
      <p className="brand-panel__name">PAPAVELAG</p>
      <p className="brand-panel__tagline">Technologies Y Soluciones S.R.L.</p>
      <p className="brand-panel__note">{note}</p>
    </div>
  </div>
)

const createSectionConfig = (t) => ({
  quotes: {
    title: t('sections.quotes.title'),
    description: t('sections.quotes.description'),
    endpoint: 'Invoices',
    singular: t('sections.quotes.singular'),
    partyLabel: t('sections.quotes.partyLabel'),
    loading: t('sections.quotes.loading'),
    empty: t('sections.quotes.empty'),
    loadError: t('sections.quotes.loadError'),
    createHeading: t('sections.quotes.createHeading'),
    createDescription: t('sections.quotes.createDescription'),
    createSuccess: t('sections.quotes.createSuccess'),
    createError: t('sections.quotes.createError'),
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

const normalizeNcfInput = (value) => {
  if (typeof value !== 'string') {
    return ''
  }

  const trimmed = value.trim()
  if (!trimmed) {
    return ''
  }

  const prefix = 'NCF'
  if (trimmed.length >= prefix.length && trimmed.slice(0, prefix.length).toUpperCase() === prefix) {
    const suffix = trimmed.slice(prefix.length)
    if (!/[A-Za-z0-9]/.test(suffix)) {
      return ''
    }
    return `${prefix}${suffix}`
  }

  if (!/[A-Za-z0-9]/.test(trimmed)) {
    return ''
  }

  const joiner = /^[A-Za-z0-9]/.test(trimmed[0]) ? ' ' : ''
  return `${prefix}${joiner}${trimmed}`
}

const DocumentList = ({
  documents,
  config,
  t,
  formatCurrency,
  formatDate,
  onDownloadPdf,
  downloadingId,
  extraActions = [],
  sortConfig,
  onSort,
  pagination,
}) => {
  const getAriaSort = (key) => {
    if (!sortConfig || sortConfig.key !== key) {
      return 'none'
    }
    return sortConfig.direction === 'asc' ? 'ascending' : 'descending'
  }

  const renderSortButton = (label, key) => {
    if (!onSort) {
      return label
    }

    const isActive = sortConfig?.key === key
    const direction = sortConfig?.direction === 'desc' ? '▼' : '▲'

    return (
      <button
        type="button"
        className={`table-sort ${isActive ? 'is-active' : ''}`}
        onClick={() => onSort(key)}
        aria-pressed={isActive}
      >
        <span>{label}</span>
        <span aria-hidden="true" className="table-sort__icon">
          {isActive ? direction : '↕'}
        </span>
      </button>
    )
  }

  return (
    <div className="document-table-wrapper">
      <table className="document-table">
        <thead>
          <tr>
            <th scope="col" aria-sort={getAriaSort('number')}>
              {renderSortButton(t('documentList.number'), 'number')}
            </th>
            <th scope="col" aria-sort={getAriaSort('date')}>
              {renderSortButton(t('documentList.date'), 'date')}
            </th>
            <th scope="col" aria-sort={getAriaSort('party')}>
              {renderSortButton(t('documentList.party'), 'party')}
            </th>
            <th scope="col" aria-sort={getAriaSort('total')}>
              {renderSortButton(t('documentList.total'), 'total')}
            </th>
            <th scope="col" aria-sort={getAriaSort('currency')}>
              {renderSortButton(t('documentList.currency'), 'currency')}
            </th>
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
                <td data-heading={t('documentList.actions')} className="document-table__actions">
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
                  {extraActions.map((action, index) => (
                    <button
                      key={`${action.label}-${index}`}
                      type="button"
                      className={`button ${action.variant ?? ''}`}
                      onClick={() => action.onClick(document)}
                      disabled={action.busyId === document.id}
                    >
                      {action.busyId === document.id
                        ? action.loadingLabel
                        : action.label}
                    </button>
                  ))}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
      {pagination ? (
        <div className="table-pagination">
          <button
            type="button"
            className="button button--secondary"
            onClick={() => pagination.onPageChange(pagination.currentPage - 1)}
            disabled={pagination.currentPage <= 1}
          >
            {t('pagination.previous')}
          </button>
          <span className="table-pagination__info">
            {t('pagination.page')} {pagination.currentPage} {t('pagination.of')} {pagination.totalPages}
          </span>
          <button
            type="button"
            className="button button--secondary"
            onClick={() => pagination.onPageChange(pagination.currentPage + 1)}
            disabled={pagination.currentPage >= pagination.totalPages}
          >
            {t('pagination.next')}
          </button>
        </div>
      ) : null}
    </div>
  )
}

function App() {
  const [language, setLanguage] = useState('es')
  const [defaultCurrency, setDefaultCurrency] = useState('USD')
  const [theme, setTheme] = useState(getInitialTheme)
  const translate = useMemo(() => createTranslator(language), [language])
  const locale = useMemo(
    () => LANGUAGES.find((entry) => entry.value === language)?.locale ?? 'en-US',
    [language],
  )
  const sectionConfig = useMemo(() => createSectionConfig(translate), [translate])

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme)
    window.localStorage.setItem('ior-theme', theme)
  }, [theme])

  const [activeSection, setActiveSection] = useState('quotes')
  const [documents, setDocuments] = useState({
    quotes: [],
    receipts: [],
  })
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [status, setStatus] = useState(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [filters, setFilters] = useState({ search: '', client: '', number: '', date: '' })
  const [downloadingId, setDownloadingId] = useState(null)
  const [invoiceGeneratingId, setInvoiceGeneratingId] = useState(null)
  const [sortConfig, setSortConfig] = useState({ key: 'date', direction: 'desc' })
  const [editingQuote, setEditingQuote] = useState(null)
  const [pageBySection, setPageBySection] = useState({ quotes: 1, receipts: 1 })
  const previewUrlRef = useRef(null)
  const [previewingId, setPreviewingId] = useState(null)

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
    if (activeSection !== 'quotes') {
      setEditingQuote(null)
    }
  }, [activeSection])

  useEffect(() => {
    setPageBySection((previous) => {
      const currentPage = previous[activeSection] ?? 1
      if (currentPage === 1) {
        return previous
      }
      return { ...previous, [activeSection]: 1 }
    })
  }, [activeSection, filters])

  useEffect(() => {
    if (previewUrlRef.current) {
      window.URL.revokeObjectURL(previewUrlRef.current)
      previewUrlRef.current = null
    }
  }, [activeSection])

  useEffect(() => {
    return () => {
      if (previewUrlRef.current) {
        window.URL.revokeObjectURL(previewUrlRef.current)
        previewUrlRef.current = null
      }
    }
  }, [])

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
        if (sectionKey === 'quotes' && responseBody?.pdfBase64) {
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

  const sortedDocuments = useMemo(() => {
    const docs = [...filteredDocuments]
    const { key, direction } = sortConfig
    const multiplier = direction === 'asc' ? 1 : -1

    const getComparableValue = (document) => {
      const partyName =
        document.partyName || document.customerName || document.supplierName || ''
      switch (key) {
        case 'number':
          return (document.number || '').toLowerCase()
        case 'date': {
          const timestamp = new Date(document.date).getTime()
          return Number.isNaN(timestamp) ? 0 : timestamp
        }
        case 'party':
          return partyName.toLowerCase()
        case 'total':
          return Number(document.totalAmount ?? 0)
        case 'currency':
          return (document.currencyCode || '').toLowerCase()
        default:
          return 0
      }
    }

    docs.sort((a, b) => {
      const valueA = getComparableValue(a)
      const valueB = getComparableValue(b)
      if (valueA === valueB) {
        return 0
      }
      return valueA > valueB ? multiplier : -multiplier
    })

    return docs
  }, [filteredDocuments, sortConfig])

  const currentPage = pageBySection[activeSection] ?? 1
  const totalPages = Math.max(1, Math.ceil(sortedDocuments.length / ROWS_PER_PAGE))

  const handlePageChange = useCallback(
    (nextPage) => {
      setPageBySection((previous) => ({
        ...previous,
        [activeSection]: Math.min(Math.max(1, nextPage), totalPages),
      }))
    },
    [activeSection, totalPages],
  )

  useEffect(() => {
    if (currentPage > totalPages) {
      handlePageChange(totalPages)
    }
  }, [currentPage, totalPages, handlePageChange])

  const pageStart = (currentPage - 1) * ROWS_PER_PAGE
  const paginatedDocuments = sortedDocuments.slice(pageStart, pageStart + ROWS_PER_PAGE)
  const paginationConfig =
    totalPages > 1
      ? {
          currentPage,
          totalPages,
          onPageChange: handlePageChange,
        }
      : null

  const currentDocuments = sortedDocuments
  const config = sectionConfig[activeSection]
  const isQuotesSection = activeSection === 'quotes'
  const isEditingQuote = isQuotesSection && Boolean(editingQuote)
  const brandNote =
    BRAND_NOTES[activeSection] ??
    BRAND_NOTES.default ??
    `Documentos ${config?.singular?.toLowerCase?.() ?? 'corporativos'} con el sello PAPAVELAG.`
  const formHeading = isEditingQuote
    ? `${translate('invoiceForm.editHeading')}${
        editingQuote?.number ? ` · ${editingQuote.number}` : ''
      }`
    : config.createHeading
  const formDescription = isEditingQuote
    ? translate('invoiceForm.editDescription')
    : config.createDescription

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

  const handlePreviewDocument = useCallback(
    async (doc) => {
      const section = sectionConfig[activeSection]
      if (!section) {
        return
      }

      try {
        setPreviewingId(doc.id)
        const response = await fetch(
          `${API_BASE_URL}/${section.endpoint}/${doc.id}/pdf`,
        )

        if (!response.ok) {
          throw new Error(translate('pdf.previewError'))
        }

        const blob = await response.blob()
        const url = window.URL.createObjectURL(blob)
        const previewWindow = window.open(url, '_blank', 'noopener')
        if (!previewWindow) {
          throw new Error(translate('preview.blocked'))
        }

        if (previewUrlRef.current) {
          window.URL.revokeObjectURL(previewUrlRef.current)
        }
        previewUrlRef.current = url
        previewWindow.focus()
      } catch (error) {
        setStatus({
          type: 'error',
          message: error.message || translate('pdf.previewError'),
        })
      } finally {
        setPreviewingId(null)
      }
    },
    [activeSection, sectionConfig, translate],
  )

  const handleEditQuote = useCallback(
    (doc) => {
      setEditingQuote(doc)
      setStatus(null)
      try {
        document.querySelector('.section-content')?.scrollIntoView({
          behavior: 'smooth',
          block: 'start',
        })
      } catch {
        // no-op if document is unavailable (e.g., during SSR)
      }
    },
    [],
  )

  const handleCancelEdit = useCallback(() => {
    setEditingQuote(null)
  }, [])

  const handleUpdateQuote = useCallback(
    async (quoteId, payload) => {
      const config = sectionConfig.quotes
      if (!config) {
        return false
      }

      setIsSubmitting(true)
      setStatus(null)

      try {
        const response = await fetch(`${API_BASE_URL}/${config.endpoint}/${quoteId}`, {
          method: 'PUT',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify(payload),
        })

        if (!response.ok) {
          let detail = translate('sections.quotes.updateError')
          try {
            const responseBody = await response.json()
            detail = responseBody?.title || responseBody?.detail || detail
          } catch {
            // Ignore parse errors and fall back to default error.
          }
          throw new Error(detail)
        }

        setStatus({ type: 'success', message: translate('sections.quotes.updateSuccess') })
        await loadSection('quotes')
        return true
      } catch (error) {
        setStatus({
          type: 'error',
          message: error.message || translate('sections.quotes.updateError'),
        })
        return false
      } finally {
        setIsSubmitting(false)
      }
    },
    [loadSection, sectionConfig, translate],
  )

  const handleQuoteSubmit = useCallback(
    async (payload) => {
      if (editingQuote) {
        const wasSuccessful = await handleUpdateQuote(editingQuote.id, payload)
        if (wasSuccessful) {
          setEditingQuote(null)
        }
        return wasSuccessful
      }

      return handleCreate('quotes', payload)
    },
    [editingQuote, handleCreate, handleUpdateQuote],
  )

  const promptForNcfNumber = useCallback(() => {
    if (typeof window === 'undefined') {
      return { cancelled: true }
    }

    const defaultValue = 'NCF '
    const userInput = window.prompt(translate('pdf.ncfPrompt'), defaultValue)

    if (userInput === null) {
      return { cancelled: true }
    }

    const normalized = normalizeNcfInput(userInput)
    if (!normalized) {
      return { cancelled: false, value: null }
    }

    return { cancelled: false, value: normalized }
  }, [translate])

  const handleGenerateInvoice = useCallback(
    async (doc) => {
      const quotesConfig = sectionConfig.quotes
      if (!quotesConfig) {
        return
      }

      const { cancelled, value } = promptForNcfNumber()
      if (cancelled) {
        return
      }

      if (!value) {
        setStatus({
          type: 'error',
          message: translate('pdf.ncfRequired'),
        })
        return
      }

      try {
        setInvoiceGeneratingId(doc.id)
        const response = await fetch(
          `${API_BASE_URL}/${quotesConfig.endpoint}/${doc.id}/invoice-pdf?ncfNumber=${encodeURIComponent(
            value,
          )}`,
        )

        if (!response.ok) {
          throw new Error(translate('pdf.invoiceDownloadError'))
        }

        const blob = await response.blob()
        const downloadUrl = window.URL.createObjectURL(blob)
        const link = window.document.createElement('a')
        const safeNumber = doc.number?.replace?.(/\s+/g, '-') ?? doc.id
        link.href = downloadUrl
        link.download = `Invoice-${safeNumber}.pdf`
        window.document.body.appendChild(link)
        link.click()
        window.document.body.removeChild(link)
        window.URL.revokeObjectURL(downloadUrl)
        setStatus({
          type: 'success',
          message: translate('pdf.invoiceGenerated'),
        })
      } catch (error) {
        setStatus({
          type: 'error',
          message: error.message || translate('pdf.invoiceDownloadError'),
        })
      } finally {
        setInvoiceGeneratingId(null)
      }
    },
    [promptForNcfNumber, sectionConfig, setStatus, translate],
  )

  const documentExtraActions = useMemo(() => {
    const previewAction = {
      label: translate('documentList.preview'),
      loadingLabel: translate('documentList.previewing'),
      busyId: previewingId,
      onClick: handlePreviewDocument,
      variant: 'button--ghost',
    }

    if (isQuotesSection) {
      return [
        {
          label: translate('documentList.edit'),
          loadingLabel: translate('documentList.editing'),
          busyId: editingQuote?.id ?? null,
          onClick: handleEditQuote,
          variant: 'button--ghost',
        },
        previewAction,
        {
          label: translate('documentList.generateInvoice'),
          loadingLabel: translate('documentList.generatingInvoice'),
          busyId: invoiceGeneratingId,
          onClick: handleGenerateInvoice,
        },
      ]
    }

    return [previewAction]
  }, [
    editingQuote?.id,
    handleEditQuote,
    handleGenerateInvoice,
    handlePreviewDocument,
    invoiceGeneratingId,
    isQuotesSection,
    previewingId,
    translate,
  ])

  const handleSort = useCallback((columnKey) => {
    setSortConfig((previous) => {
      const nextDirection =
        previous.key === columnKey && previous.direction === 'asc' ? 'desc' : 'asc'
      return { key: columnKey, direction: nextDirection }
    })
  }, [])

  return (
    <div className="layout">
      <header className="page-header">
        <img
          src={PapavelagLogo}
          alt="Papavelag Technologies logo"
          className="page-header__logo"
        />
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
        <label className="toolbar__control">
          {translate('controls.theme')}
          <select value={theme} onChange={(event) => setTheme(event.target.value)}>
            <option value="light">{translate('controls.themeLight')}</option>
            <option value="dark">{translate('controls.themeDark')}</option>
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
        <BrandPanel note={brandNote} variant="compact" />

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
              {activeSection === 'quotes'
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
            documents={paginatedDocuments}
            config={config}
            t={translate}
            formatCurrency={formatCurrency}
            formatDate={formatDate}
            onDownloadPdf={handleDownloadFromList}
            downloadingId={downloadingId}
            sortConfig={sortConfig}
            onSort={handleSort}
            extraActions={documentExtraActions}
            pagination={paginationConfig}
          />
        )}
      </section>

      <section className="section-content">
        <div className="section-intro">
          <h2>{formHeading}</h2>
          <p>{formDescription}</p>
        </div>
        <BrandPanel note={brandNote} />

        {isQuotesSection ? (
          <InvoiceForm
            onSubmit={handleQuoteSubmit}
            isSubmitting={isSubmitting}
            t={translate}
            defaultCurrency={defaultCurrency}
            locale={locale}
            initialInvoice={editingQuote}
            mode={isEditingQuote ? 'edit' : 'create'}
            onCancelEdit={isEditingQuote ? handleCancelEdit : undefined}
          />
        ) : (
          <ReceiptForm
            onSubmit={(payload) => handleCreate('receipts', payload)}
            isSubmitting={isSubmitting}
            t={translate}
          />
        )}
      </section>
    </div>
  )
}

export default App
