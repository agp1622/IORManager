import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import InvoiceForm from './components/InvoiceForm'
import ReceiptForm from './components/ReceiptForm'
import Modal from './components/Modal'
import './App.css'
import { createTranslator, LANGUAGES } from './i18n'
import PapavelagLogo from './assets/papavelag-logo.svg'
import {
  DEFAULT_NCF_CATEGORY,
  inferNcfCategoryFromNumber,
  getNcfSequenceLength,
  NCF_CATEGORY_OPTIONS,
  normalizeNcfCategory,
} from './constants/ncfCategories'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5031/api'
const CURRENCY_CODES = ['USD', 'DOP']
const ROWS_PER_PAGE = 10

const BRAND_NOTES = {
  quotes: 'Cotizaciones impactantes listas para convertirse en facturas.',
  invoices: 'Facturas fiscales listas para control, descarga y seguimiento.',
  receipts: 'Recibos elegantes que transmiten confianza y precisión.',
  default: 'Documentos oficiales con el sello PAPAVELAG.',
}

const getCompactNcfToken = (value) => {
  if (typeof value !== 'string') {
    return ''
  }

  const compact = value.toUpperCase().replace(/[\s-]/g, '')
  return compact.startsWith('NCF') ? compact.slice(3) : compact
}

const sanitizeNcfSuffix = (value) => getCompactNcfToken(value).replace(/\D/g, '')

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

const UiIcon = ({ children, className = 'ui-icon' }) => (
  <svg
    className={className}
    viewBox="0 0 20 20"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.8"
    strokeLinecap="round"
    strokeLinejoin="round"
    aria-hidden="true"
    focusable="false"
  >
    {children}
  </svg>
)

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
    endpoint: 'Quotes',
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
  extraActions = [],
  sortConfig,
  onSort,
  pagination,
}) => {
  const isQuoteList = config?.endpoint === 'Quotes'
  const isInvoiceList = config?.endpoint === 'Invoices'
  const [openActionsId, setOpenActionsId] = useState(null)

  useEffect(() => {
    if (!openActionsId) {
      return undefined
    }

    const handleMouseDown = (event) => {
      const target = event.target
      if (!(target instanceof Element)) {
        return
      }

      if (target.closest('.document-table__actions')) {
        return
      }

      setOpenActionsId(null)
    }

    const handleKeyDown = (event) => {
      if (event.key === 'Escape') {
        setOpenActionsId(null)
      }
    }

    document.addEventListener('mousedown', handleMouseDown)
    document.addEventListener('keydown', handleKeyDown)
    return () => {
      document.removeEventListener('mousedown', handleMouseDown)
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [openActionsId])

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
            {isInvoiceList ? (
              <th scope="col" aria-sort={getAriaSort('quoteNumber')}>
                {renderSortButton(t('documentList.quoteNumber'), 'quoteNumber')}
              </th>
            ) : null}
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
            <th scope="col" aria-sort={getAriaSort('ncf')}>
              {renderSortButton(t('documentList.ncfLabel'), 'ncf')}
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
            const invoiceGenerated = Boolean(
              document.convertedAt ||
              document.convertedInvoiceId ||
              document.invoiceGeneratedAt ||
              document.ncfNumber ||
              document.invoiceGenerated,
            )
            const ncfNumber = document.ncfNumber
            const quoteNumber = document.quoteNumber || '—'

            return (
              <tr key={document.id}>
                <td data-heading={t('documentList.number')}>
                  <div className="document-number">
                    <span>{document.number}</span>
                    {isQuoteList && invoiceGenerated ? (
                      <span className="pill pill--success">{t('documentList.invoiceReady')}</span>
                    ) : null}
                    {isQuoteList && ncfNumber ? (
                      <span className="pill pill--muted">
                        {ncfNumber}
                      </span>
                    ) : null}
                  </div>
                </td>
                {isInvoiceList ? (
                  <td data-heading={t('documentList.quoteNumber')}>
                    {quoteNumber}
                  </td>
                ) : null}
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
                <td data-heading={t('documentList.ncfLabel')}>
                  {ncfNumber || '—'}
                </td>
                <td data-heading={t('documentList.actions')} className="document-table__actions">
                  <button
                    type="button"
                    className="button button--secondary document-actions-toggle"
                    onClick={() =>
                      setOpenActionsId((current) => (current === document.id ? null : document.id))
                    }
                    aria-haspopup="menu"
                    aria-expanded={openActionsId === document.id}
                    aria-label={t('documentList.actions')}
                    title={t('documentList.actions')}
                  >
                    <span aria-hidden="true">⋯</span>
                  </button>

                  {openActionsId === document.id ? (
                    <div className="document-actions-menu" role="menu" aria-label={t('documentList.actions')}>
                      <button
                        type="button"
                        className="document-actions-menu__item"
                        onClick={() => {
                          setOpenActionsId(null)
                          onDownloadPdf(document)
                        }}
                        disabled={isDownloading}
                      >
                        {isDownloading ? t('documentList.downloading') : t('documentList.downloadPdf')}
                      </button>
                      {extraActions.map((action, index) => {
                        const shouldRender =
                          typeof action.isVisible === 'function' ? action.isVisible(document) : true
                        if (!shouldRender) {
                          return null
                        }

                        const resolveLabel = (value) =>
                          typeof value === 'function' ? value(document) : value
                        const isBusy = action.busyId === document.id
                        const actionLabel = resolveLabel(action.label)
                        const loadingLabel = resolveLabel(action.loadingLabel)

                        return (
                          <button
                            key={`${actionLabel}-${index}`}
                            type="button"
                            className="document-actions-menu__item"
                            onClick={() => {
                              setOpenActionsId(null)
                              action.onClick(document)
                            }}
                            disabled={isBusy}
                          >
                            {isBusy ? loadingLabel : actionLabel}
                          </button>
                        )
                      })}
                    </div>
                  ) : null}
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
    invoices: [],
    receipts: [],
  })
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [status, setStatus] = useState(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [filters, setFilters] = useState({ search: '', client: '', number: '', date: '', ncf: '' })
  const [downloadingId, setDownloadingId] = useState(null)
  const [invoiceGeneratingId, setInvoiceGeneratingId] = useState(null)
  const [undoingQuoteId, setUndoingQuoteId] = useState(null)
  const [updatingNcfId, setUpdatingNcfId] = useState(null)
  const [duplicatingQuoteId, setDuplicatingQuoteId] = useState(null)
  const [apiNcfCategories, setApiNcfCategories] = useState([])
  const [customers, setCustomers] = useState([])
  const [sortConfig, setSortConfig] = useState({ key: 'date', direction: 'desc' })
  const [editingQuote, setEditingQuote] = useState(null)
  const [isQuoteModalOpen, setIsQuoteModalOpen] = useState(false)
  const [pageBySection, setPageBySection] = useState({ quotes: 1, invoices: 1, receipts: 1 })
  const previewUrlRef = useRef(null)
  const [previewingId, setPreviewingId] = useState(null)
  const [ncfDialog, setNcfDialog] = useState({
    isOpen: false,
    mode: 'generate',
    document: null,
    invoiceId: null,
    suffix: '',
    category: DEFAULT_NCF_CATEGORY,
    skipNcf: false,
    isLoading: false,
    error: null,
  })

  const ncfCategories = useMemo(() => {
    if (apiNcfCategories.length > 0) {
      return apiNcfCategories
    }

    return NCF_CATEGORY_OPTIONS.map((option) => ({
      code: option.code,
      sequenceLength: option.sequenceLength,
      isElectronic: option.code.startsWith('E'),
      name: translate(`ncfCategories.${option.code}`),
    }))
  }, [apiNcfCategories, translate])

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

  const loadCustomers = useCallback(async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/Customers`)
      if (!response.ok) {
        return
      }

      const data = await response.json()
      if (!Array.isArray(data)) {
        return
      }

      const byName = new Map()
      data.forEach((customer) => {
        const normalized = {
          id: customer?.id || '',
          name: typeof customer?.name === 'string' ? customer.name.trim() : '',
          address: typeof customer?.address === 'string' ? customer.address.trim() : '',
          contact: typeof customer?.contact === 'string' ? customer.contact.trim() : '',
        }

        if (!normalized.name) {
          return
        }

        const key = normalized.name.toLowerCase()
        if (!byName.has(key)) {
          byName.set(key, normalized)
        }
      })

      setCustomers(
        Array.from(byName.values()).sort((left, right) =>
          left.name.localeCompare(right.name, undefined, { sensitivity: 'base' }),
        ),
      )
    } catch {
      // Keep autocomplete optional if the customer endpoint is unavailable.
    }
  }, [])

  useEffect(() => {
    loadSection(activeSection)
  }, [activeSection, loadSection])

  useEffect(() => {
    loadCustomers()
  }, [loadCustomers])

  useEffect(() => {
    let isMounted = true

    const loadNcfCategories = async () => {
      try {
        const response = await fetch(`${API_BASE_URL}/Invoices/ncf-categories`)
        if (!response.ok) {
          return
        }

        const data = await response.json()
        if (!isMounted || !Array.isArray(data)) {
          return
        }

        const normalized = data
          .map((entry) => ({
            code: normalizeNcfCategory(entry?.code, NCF_CATEGORY_OPTIONS),
            name: typeof entry?.name === 'string' ? entry.name.trim() : '',
            sequenceLength: Number(entry?.sequenceLength) || 8,
            isElectronic: Boolean(entry?.isElectronic),
          }))
          .filter((entry) => Boolean(entry.code))

        if (normalized.length > 0) {
          setApiNcfCategories(normalized)
        }
      } catch {
        // Ignore category-loading errors and keep local fallback options.
      }
    }

    loadNcfCategories()
    return () => {
      isMounted = false
    }
  }, [])

  useEffect(() => {
    setFilters({ search: '', client: '', number: '', date: '', ncf: '' })
  }, [activeSection])

  useEffect(() => {
    if (activeSection !== 'quotes') {
      setEditingQuote(null)
      setIsQuoteModalOpen(false)
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
        if (sectionKey === 'quotes') {
          await loadCustomers()
        }
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
    [loadCustomers, loadSection, sectionConfig, translate, triggerPdfDownload],
  )

  const filteredDocuments = useMemo(() => {
    const source = documents[activeSection] ?? []
    const searchTerm = filters.search.trim().toLowerCase()
    const clientTerm = filters.client.trim().toLowerCase()
    const numberTerm = filters.number.trim().toLowerCase()
    const dateTerm = filters.date
    const ncfTerm = filters.ncf.trim().toLowerCase()

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

    if (!searchTerm && !clientTerm && !numberTerm && !dateTerm && !ncfTerm) {
      return source
    }

    return source.filter((document) => {
      const partyName =
        document.partyName || document.customerName || document.supplierName || ''
      const partyLower = partyName.toLowerCase()
      const numberLower = (document.number || '').toLowerCase()
      const quoteNumberLower = (document.quoteNumber || '').toLowerCase()
      const normalizedDate = normalizeDate(document.date)
      const normalizedNcf = (document.ncfNumber || '').toLowerCase()

      const matchesClient = !clientTerm || partyLower.includes(clientTerm)
      const matchesNumber =
        !numberTerm || numberLower.includes(numberTerm) || quoteNumberLower.includes(numberTerm)
      const matchesDate = !dateTerm || normalizedDate === dateTerm
      const matchesNcf = !ncfTerm || normalizedNcf.includes(ncfTerm)

      let matchesSearch = true
      if (searchTerm) {
        const searchable = [
          numberLower,
          quoteNumberLower,
          partyLower,
          (document.referenceNumber || '').toLowerCase(),
          (document.currencyCode || '').toLowerCase(),
          (document.id || '').toLowerCase(),
          (document.customerAddress || '').toLowerCase(),
          (document.customerContact || '').toLowerCase(),
          (document.ncfNumber || '').toLowerCase(),
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

      return matchesClient && matchesNumber && matchesDate && matchesNcf && matchesSearch
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
        case 'quoteNumber':
          return (document.quoteNumber || '').toLowerCase()
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
        case 'ncf':
          return (document.ncfNumber || '').toLowerCase()
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
  const isInvoicesSection = activeSection === 'invoices'
  const isReceiptsSection = activeSection === 'receipts'
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
      setIsQuoteModalOpen(true)
    },
    [],
  )

  const handleOpenQuoteModal = useCallback(() => {
    setEditingQuote(null)
    setStatus(null)
    setIsQuoteModalOpen(true)
  }, [])

  const handleResumeEdit = useCallback(() => {
    setIsQuoteModalOpen(true)
  }, [])

  const handleCloseQuoteModal = useCallback(() => {
    setIsQuoteModalOpen(false)
  }, [])

  const handleCancelEdit = useCallback(() => {
    setEditingQuote(null)
    setIsQuoteModalOpen(false)
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
        await loadCustomers()
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
    [loadCustomers, loadSection, sectionConfig, translate],
  )

  const handleDuplicateQuote = useCallback(
    async (doc) => {
      if (!doc?.id) {
        return
      }

      try {
        setDuplicatingQuoteId(doc.id)
        const response = await fetch(`${API_BASE_URL}/Quotes/${doc.id}/duplicate`, {
          method: 'POST',
        })

        if (!response.ok) {
          let detail = translate('sections.quotes.duplicateError')
          try {
            const body = await response.json()
            detail = body?.title || body?.detail || detail
          } catch {
            // Ignore parse errors and use fallback text.
          }
          throw new Error(detail)
        }

        setStatus({ type: 'success', message: translate('sections.quotes.duplicateSuccess') })
        await loadSection('quotes')
        await loadCustomers()
      } catch (error) {
        setStatus({
          type: 'error',
          message: error.message || translate('sections.quotes.duplicateError'),
        })
      } finally {
        setDuplicatingQuoteId(null)
      }
    },
    [loadCustomers, loadSection, translate],
  )

  const handleQuoteSubmit = useCallback(
    async (payload) => {
      if (editingQuote) {
        const wasSuccessful = await handleUpdateQuote(editingQuote.id, payload)
        if (wasSuccessful) {
          setEditingQuote(null)
          setIsQuoteModalOpen(false)
        }
        return wasSuccessful
      }

      const created = await handleCreate('quotes', payload)
      if (created) {
        setIsQuoteModalOpen(false)
      }
      return created
    },
    [editingQuote, handleCreate, handleUpdateQuote],
  )

  const downloadInvoicePdfById = useCallback(
    async (invoiceId, fileNameSeed) => {
      const response = await fetch(`${API_BASE_URL}/Invoices/${invoiceId}/pdf`)
      if (!response.ok) {
        throw new Error(translate('pdf.invoiceDownloadError'))
      }

      const blob = await response.blob()
      const downloadUrl = window.URL.createObjectURL(blob)
      const link = window.document.createElement('a')
      const safeNumber = fileNameSeed?.replace?.(/\s+/g, '-') ?? invoiceId
      link.href = downloadUrl
      link.download = `Invoice-${safeNumber}.pdf`
      window.document.body.appendChild(link)
      link.click()
      window.document.body.removeChild(link)
      window.URL.revokeObjectURL(downloadUrl)
    },
    [translate],
  )

  const convertQuoteToInvoice = useCallback(
    async (quoteId, ncfValue, ncfCategory, skipNcf = false) => {
      const normalizedCategory =
        normalizeNcfCategory(ncfCategory, ncfCategories) || DEFAULT_NCF_CATEGORY

      const response = await fetch(`${API_BASE_URL}/Quotes/${quoteId}/convert`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(
          skipNcf
            ? { skipNcf: true }
            : { ncfNumber: ncfValue || null, ncfCategory: normalizedCategory },
        ),
      })

      const body = await response.json().catch(() => null)
      if (!response.ok) {
        const detail = body?.title || body?.detail || body?.message || translate('pdf.ncfAssignError')
        throw new Error(detail)
      }

      return {
        invoiceId: body?.invoiceId || body?.id || '',
        invoiceNumber: body?.invoiceNumber || body?.number || '',
        ncfNumber: body?.ncfNumber || body?.ncf || body?.value || '',
        ncfCategory:
          normalizeNcfCategory(body?.ncfCategory || body?.category, ncfCategories) || normalizedCategory,
      }
    },
    [ncfCategories, translate],
  )

  const updateInvoiceNcf = useCallback(
    async (invoiceId, ncfValue, ncfCategory) => {
      const normalizedCategory =
        normalizeNcfCategory(ncfCategory, ncfCategories) || DEFAULT_NCF_CATEGORY

      const response = await fetch(`${API_BASE_URL}/Invoices/${invoiceId}/ncf`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ncfNumber: ncfValue || null,
          ncfCategory: normalizedCategory,
        }),
      })

      const body = await response.json().catch(() => null)
      if (!response.ok) {
        const detail = body?.title || body?.detail || body?.message || translate('pdf.ncfAssignError')
        throw new Error(detail)
      }

      return {
        ncfNumber: body?.ncfNumber || body?.ncf || body?.value || '',
        ncfCategory:
          normalizeNcfCategory(body?.ncfCategory || body?.category, ncfCategories) || normalizedCategory,
      }
    },
    [ncfCategories, translate],
  )

  const undoQuoteConversion = useCallback(
    async (quoteId) => {
      const response = await fetch(`${API_BASE_URL}/Quotes/${quoteId}/undo-conversion`, {
        method: 'POST',
      })

      const body = await response.json().catch(() => null)
      if (!response.ok) {
        const detail = body?.title || body?.detail || body?.message || translate('sections.quotes.undoInvoiceError')
        throw new Error(detail)
      }
    },
    [translate],
  )

  const fetchInvoiceNcfData = useCallback(
    async (invoiceId) => {
      const response = await fetch(`${API_BASE_URL}/Invoices/${invoiceId}`)
      const body = await response.json().catch(() => null)

      if (!response.ok) {
        const detail = body?.title || body?.detail || body?.message || translate('pdf.ncfAssignError')
        throw new Error(detail)
      }

      return {
        ncfNumber: body?.ncfNumber || '',
        ncfCategory:
          normalizeNcfCategory(body?.ncfCategory || body?.category, ncfCategories) || null,
      }
    },
    [ncfCategories, translate],
  )

  const extractNcfSuffix = useCallback(
    (ncfValue, ncfCategory) => {
      const normalizedCategory =
        normalizeNcfCategory(ncfCategory, ncfCategories) || DEFAULT_NCF_CATEGORY
      const compact = getCompactNcfToken(ncfValue)
      if (!compact) {
        return ''
      }

      if (compact.startsWith(normalizedCategory)) {
        return compact.slice(normalizedCategory.length)
      }

      const inferredCategory = inferNcfCategoryFromNumber(compact, ncfCategories)
      if (inferredCategory && compact.startsWith(inferredCategory)) {
        return compact.slice(inferredCategory.length)
      }

      return compact
    },
    [ncfCategories],
  )

  const buildNcfNumber = useCallback(
    (ncfCategory, ncfSuffix) => {
      const normalizedCategory =
        normalizeNcfCategory(ncfCategory, ncfCategories) || DEFAULT_NCF_CATEGORY
      const sequenceLength = getNcfSequenceLength(normalizedCategory, ncfCategories)
      const sanitizedSuffix = sanitizeNcfSuffix(ncfSuffix).slice(0, sequenceLength)
      return sanitizedSuffix ? `${normalizedCategory}${sanitizedSuffix}` : ''
    },
    [ncfCategories],
  )

  const handleGenerateInvoice = useCallback(
    async (doc, ncfOverride = null, categoryOverride = null, skipNcf = false) => {
      if (!doc?.id) {
        return
      }

      try {
        setInvoiceGeneratingId(doc.id)
        if (doc.convertedInvoiceId) {
          await downloadInvoicePdfById(doc.convertedInvoiceId, doc.number || doc.id)
        } else {
          const conversion = await convertQuoteToInvoice(doc.id, ncfOverride, categoryOverride, skipNcf)
          await downloadInvoicePdfById(conversion.invoiceId, conversion.invoiceNumber || doc.number || doc.id)
        }

        setStatus({
          type: 'success',
          message: translate('pdf.invoiceGenerated'),
        })
        await Promise.all([loadSection('quotes'), loadSection('invoices')])
      } catch (error) {
        const message = error?.message || translate('pdf.invoiceDownloadError')
        setStatus({
          type: 'error',
          message,
        })
        throw new Error(message)
      } finally {
        setInvoiceGeneratingId(null)
      }
    },
    [convertQuoteToInvoice, downloadInvoicePdfById, loadSection, setStatus, translate],
  )

  const fetchSuggestedNcf = useCallback(async (requestedCategory) => {
    const normalizedCategory =
      normalizeNcfCategory(requestedCategory, ncfCategories) || DEFAULT_NCF_CATEGORY
    const response = await fetch(
      `${API_BASE_URL}/Invoices/next-ncf?ncfCategory=${encodeURIComponent(normalizedCategory)}`,
    )
    const body = await response.json().catch(() => null)

    if (!response.ok) {
      const detail =
        body?.title || body?.detail || body?.message || translate('pdf.ncfAssignError')
      throw new Error(detail)
    }

    return {
      ncfNumber: body?.ncfNumber || body?.ncf || body?.value || '',
      ncfCategory:
        normalizeNcfCategory(body?.ncfCategory || body?.category, ncfCategories) || normalizedCategory,
    }
  }, [ncfCategories, translate])

  const handleOpenNcfDialog = useCallback(
    async (doc, mode = 'generate') => {
      if (!doc?.id) {
        return
      }

      const editingNcf = mode === 'edit'
      const resolvedInvoiceId = editingNcf
        ? (activeSection === 'invoices' ? doc.id : doc.convertedInvoiceId || null)
        : null
      const existingValue = doc?.ncfNumber || ''
      const existingCategory =
        normalizeNcfCategory(doc?.ncfCategory, ncfCategories) ||
        inferNcfCategoryFromNumber(existingValue, ncfCategories) ||
        DEFAULT_NCF_CATEGORY
      const shouldFetchSuggestion = !editingNcf && !existingValue

      setNcfDialog({
        isOpen: true,
        mode,
        document: doc,
        invoiceId: resolvedInvoiceId,
        suffix: extractNcfSuffix(existingValue, existingCategory),
        category: existingCategory,
        isLoading: editingNcf,
        error: null,
      })

      if (editingNcf) {
        if (!resolvedInvoiceId) {
          setNcfDialog((current) => ({
            ...current,
            isLoading: false,
            error: translate('pdf.ncfEditMissingInvoice'),
          }))
          return
        }

        try {
          const invoiceNcf = await fetchInvoiceNcfData(resolvedInvoiceId)
          setNcfDialog((current) => {
            if (!current.isOpen || current.document?.id !== doc.id || current.mode !== mode) {
              return current
            }

            const category =
              invoiceNcf.ncfCategory ||
              inferNcfCategoryFromNumber(invoiceNcf.ncfNumber, ncfCategories) ||
              current.category
            return {
              ...current,
              category,
              suffix: extractNcfSuffix(invoiceNcf.ncfNumber, category),
              isLoading: false,
              error: null,
            }
          })
        } catch (error) {
          setNcfDialog((current) => {
            if (!current.isOpen || current.document?.id !== doc.id || current.mode !== mode) {
              return current
            }

            return {
              ...current,
              isLoading: false,
              error: error.message || translate('pdf.ncfAssignError'),
            }
          })
        }

        return
      }

      if (!shouldFetchSuggestion) {
        return
      }

      try {
        const suggested = await fetchSuggestedNcf(existingCategory)
        setNcfDialog((current) => {
          if (!current.isOpen || current.document?.id !== doc?.id) {
            return current
          }

          if (current.suffix.trim()) {
            return current
          }

          return {
            ...current,
            category: suggested.ncfCategory,
            suffix: extractNcfSuffix(suggested.ncfNumber, suggested.ncfCategory),
          }
        })
      } catch (error) {
        setNcfDialog((current) => {
          if (!current.isOpen || current.document?.id !== doc?.id) {
            return current
          }

          return {
            ...current,
            error: error.message || translate('pdf.ncfAssignError'),
          }
        })
      }
    },
    [activeSection, extractNcfSuffix, fetchInvoiceNcfData, fetchSuggestedNcf, ncfCategories, translate],
  )

  const handleCancelNcfDialog = useCallback(() => {
    setNcfDialog({
      isOpen: false,
      mode: 'generate',
      document: null,
      invoiceId: null,
      suffix: '',
      category: DEFAULT_NCF_CATEGORY,
      skipNcf: false,
      isLoading: false,
      error: null,
    })
  }, [])

  const handleConfirmNcfDialog = useCallback(async () => {
    if (!ncfDialog.document) {
      return
    }

    const normalizedCategory =
      normalizeNcfCategory(ncfDialog.category, ncfCategories) || DEFAULT_NCF_CATEGORY
    const ncfNumber = buildNcfNumber(normalizedCategory, ncfDialog.suffix)
    if (!ncfDialog.skipNcf && !ncfNumber) {
      setNcfDialog((current) => ({
        ...current,
        error: translate('pdf.ncfRequired'),
      }))
      return
    }

    try {
      setNcfDialog((current) => ({ ...current, isLoading: true, error: null }))
      if (ncfDialog.mode === 'edit') {
        if (!ncfDialog.invoiceId) {
          throw new Error(translate('pdf.ncfEditMissingInvoice'))
        }

        setUpdatingNcfId(ncfDialog.document.id)
        await updateInvoiceNcf(ncfDialog.invoiceId, ncfNumber, normalizedCategory)
        setStatus({
          type: 'success',
          message: translate('pdf.ncfUpdated'),
        })
      } else {
        await handleGenerateInvoice(
          ncfDialog.document,
          ncfDialog.skipNcf ? null : ncfNumber,
          ncfDialog.skipNcf ? null : normalizedCategory,
          ncfDialog.skipNcf,
        )
      }

      setNcfDialog({
        isOpen: false,
        mode: 'generate',
        document: null,
        invoiceId: null,
        suffix: '',
        category: DEFAULT_NCF_CATEGORY,
        skipNcf: false,
        isLoading: false,
        error: null,
      })
      await Promise.all([loadSection('quotes'), loadSection('invoices')])
    } catch (error) {
      setNcfDialog((current) => ({
        ...current,
        isLoading: false,
        error: error.message || translate('pdf.ncfAssignError'),
      }))
    } finally {
      setUpdatingNcfId(null)
    }
  }, [
    buildNcfNumber,
    handleGenerateInvoice,
    loadSection,
    ncfCategories,
    ncfDialog.category,
    ncfDialog.document,
    ncfDialog.invoiceId,
    ncfDialog.mode,
    ncfDialog.skipNcf,
    ncfDialog.suffix,
    translate,
    updateInvoiceNcf,
  ])

  const handleUndoConvertedInvoice = useCallback(
    async (doc) => {
      if (!doc?.id) {
        return
      }

      try {
        setUndoingQuoteId(doc.id)
        await undoQuoteConversion(doc.id)
        setStatus({
          type: 'success',
          message: translate('sections.quotes.undoInvoiceSuccess'),
        })
        await Promise.all([loadSection('quotes'), loadSection('invoices')])
      } catch (error) {
        setStatus({
          type: 'error',
          message: error.message || translate('sections.quotes.undoInvoiceError'),
        })
      } finally {
        setUndoingQuoteId(null)
      }
    },
    [loadSection, translate, undoQuoteConversion],
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
          label: translate('documentList.duplicateQuote'),
          loadingLabel: translate('documentList.duplicatingQuote'),
          busyId: duplicatingQuoteId,
          onClick: handleDuplicateQuote,
          variant: 'button--ghost',
        },
        {
          label: (doc) =>
            doc?.convertedInvoiceId || doc?.convertedAt || doc?.invoiceGeneratedAt || doc?.ncfNumber
              ? translate('documentList.downloadInvoice')
              : translate('documentList.generateInvoice'),
          loadingLabel: translate('documentList.generatingInvoice'),
          busyId: invoiceGeneratingId,
          onClick: (doc) => {
            if (doc?.convertedInvoiceId || doc?.convertedAt) {
              void handleGenerateInvoice(doc).catch(() => {})
              return
            }

            void handleOpenNcfDialog(doc, 'generate')
          },
          variant: 'button--ghost',
        },
        {
          label: translate('documentList.editNcf'),
          loadingLabel: translate('documentList.editingNcf'),
          busyId: updatingNcfId,
          isVisible: (doc) => Boolean(doc?.convertedInvoiceId || doc?.convertedAt),
          onClick: (doc) => {
            void handleOpenNcfDialog(doc, 'edit')
          },
          variant: 'button--ghost',
        },
        {
          label: translate('documentList.undoInvoice'),
          loadingLabel: translate('documentList.undoingInvoice'),
          busyId: undoingQuoteId,
          isVisible: (doc) => Boolean(doc?.convertedInvoiceId || doc?.convertedAt),
          onClick: handleUndoConvertedInvoice,
          variant: 'button--ghost',
        },
      ]
    }

    if (isInvoicesSection) {
      return [
        previewAction,
        {
          label: translate('documentList.editNcf'),
          loadingLabel: translate('documentList.editingNcf'),
          busyId: updatingNcfId,
          onClick: (doc) => {
            void handleOpenNcfDialog(doc, 'edit')
          },
          variant: 'button--ghost',
        },
      ]
    }

    return [previewAction]
  }, [
    duplicatingQuoteId,
    editingQuote?.id,
    handleDuplicateQuote,
    handleEditQuote,
    handleGenerateInvoice,
    handleOpenNcfDialog,
    handlePreviewDocument,
    handleUndoConvertedInvoice,
    invoiceGeneratingId,
    isInvoicesSection,
    isQuotesSection,
    previewingId,
    translate,
    undoingQuoteId,
    updatingNcfId,
  ])

  const handleSort = useCallback((columnKey) => {
    setSortConfig((previous) => {
      const nextDirection =
        previous.key === columnKey && previous.direction === 'asc' ? 'desc' : 'asc'
      return { key: columnKey, direction: nextDirection }
    })
  }, [])

  const ncfDialogSequenceLength = getNcfSequenceLength(ncfDialog.category, ncfCategories)
  const ncfDialogSuffixPlaceholder = '0'.repeat(ncfDialogSequenceLength)
  const isNcfEditMode = ncfDialog.mode === 'edit'

  return (
    <div className="layout">
      <header className="top-nav">
        <div className="top-nav__brand">
          <img
            src={PapavelagLogo}
            alt="Papavelag Technologies logo"
            className="top-nav__logo"
          />
          <span className="top-nav__title">{translate('app.title')}</span>
        </div>
        <div className="top-nav__controls">
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
      </header>

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
        <div className={`section-intro ${isQuotesSection ? 'section-intro--with-action' : ''}`}>
          <div className="section-intro__copy">
            <h2>{config.title}</h2>
            <p>{config.description}</p>
          </div>
          {isQuotesSection ? (
            <button
              type="button"
              className="button button--compact button--icon-text"
              onClick={isEditingQuote ? handleResumeEdit : handleOpenQuoteModal}
            >
              <UiIcon className="ui-icon ui-icon--button">
                <path d="M10 4v12" />
                <path d="M4 10h12" />
              </UiIcon>
              {isEditingQuote
                ? translate('invoiceForm.resumeEdit')
                : translate('invoiceForm.openModal')}
            </button>
          ) : null}
        </div>

        <div className="filter-bar" role="search">
          <div className="filter-bar__grid">
            <label>
              <span className="field-label">
                <UiIcon>
                  <circle cx="8.5" cy="8.5" r="4.5" />
                  <path d="M12 12l4 4" />
                </UiIcon>
                <span>{translate('filters.searchLabel')}</span>
              </span>
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
              <span className="field-label">
                <UiIcon>
                  <path d="M14.5 16.5a4.5 4.5 0 0 0-9 0" />
                  <circle cx="10" cy="7" r="3" />
                </UiIcon>
                <span>{config.partyLabel}</span>
              </span>
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
              <span className="field-label">
                <UiIcon>
                  <path d="M6 4L4 16" />
                  <path d="M12 4l-2 12" />
                  <path d="M3 8h12" />
                  <path d="M2 12h12" />
                </UiIcon>
                <span>
                  {activeSection === 'quotes'
                    ? translate('filters.numberLabel')
                    : translate('filters.genericNumberLabel')}
                </span>
              </span>
              <input
                type="text"
                value={filters.number}
                onChange={(event) =>
                  setFilters((current) => ({ ...current, number: event.target.value }))
                }
              />
            </label>
            <label>
              <span className="field-label">
                <UiIcon>
                  <rect x="3" y="4.5" width="14" height="12" rx="2" />
                  <path d="M6.5 3v3" />
                  <path d="M13.5 3v3" />
                  <path d="M3 8.5h14" />
                </UiIcon>
                <span>{translate('filters.dateLabel')}</span>
              </span>
              <input
                type="date"
                value={filters.date}
                onChange={(event) =>
                  setFilters((current) => ({ ...current, date: event.target.value }))
                }
              />
            </label>
            <label>
              <span className="field-label">
                <UiIcon>
                  <path d="M6 4.5h6.8a1.2 1.2 0 0 1 .85.35l1.95 1.95a1.2 1.2 0 0 1 .35.85v4.7a1.2 1.2 0 0 1-1.2 1.2H6a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2z" />
                  <circle cx="12.8" cy="7.3" r="0.7" />
                </UiIcon>
                <span>{translate('filters.ncfLabel')}</span>
              </span>
              <input
                type="text"
                value={filters.ncf}
                onChange={(event) =>
                  setFilters((current) => ({ ...current, ncf: event.target.value }))
                }
                placeholder={translate('filters.ncfPlaceholder')}
              />
            </label>
          </div>
          <button
            type="button"
            className="button button--secondary button--compact button--icon-text"
            onClick={() => setFilters({ search: '', client: '', number: '', date: '', ncf: '' })}
          >
            <UiIcon className="ui-icon ui-icon--button">
              <path d="M16 5v4h-4" />
              <path d="M4.8 8.2a6 6 0 1 1 .3 5.8" />
            </UiIcon>
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

      {!isQuotesSection ? (
        <section className="section-content">
          <div className="section-intro">
            <h2>{formHeading}</h2>
            <p>{formDescription}</p>
          </div>
          <BrandPanel note={brandNote} />

          {isReceiptsSection ? (
            <ReceiptForm
              onSubmit={(payload) => handleCreate('receipts', payload)}
              isSubmitting={isSubmitting}
              t={translate}
            />
          ) : (
            <div className="quote-cta">
              <div className="quote-cta__copy">
                <p>{translate('sections.invoices.panelHint')}</p>
              </div>
              <div className="quote-cta__actions">
                <button
                  type="button"
                  className="button"
                  onClick={() => setActiveSection('quotes')}
                >
                  {translate('sections.invoices.openQuotes')}
                </button>
              </div>
            </div>
          )}
        </section>
      ) : null}

      {isQuotesSection ? (
        <Modal
          isOpen={isQuoteModalOpen}
          onClose={handleCloseQuoteModal}
          title={formHeading}
          description={formDescription}
          eyebrow={config.title}
        >
          <InvoiceForm
            onSubmit={handleQuoteSubmit}
            isSubmitting={isSubmitting}
            t={translate}
            defaultCurrency={defaultCurrency}
            locale={locale}
            initialInvoice={editingQuote}
            mode={isEditingQuote ? 'edit' : 'create'}
            onCancelEdit={isEditingQuote ? handleCancelEdit : undefined}
            customers={customers}
          />
        </Modal>
      ) : null}
      {ncfDialog.isOpen ? (
        <Modal
          isOpen={ncfDialog.isOpen}
          onClose={ncfDialog.isLoading ? undefined : handleCancelNcfDialog}
          title={translate(isNcfEditMode ? 'pdf.ncfEditDialogTitle' : 'pdf.ncfDialogTitle')}
          description={translate(isNcfEditMode ? 'pdf.ncfEditDialogDescription' : 'pdf.ncfDialogDescription')}
          eyebrow={config.title}
        >
          <div className="modal-body">
            {ncfDialog.error ? (
              <div className="banner banner--error" role="alert">
                {ncfDialog.error}
              </div>
            ) : null}
            {!isNcfEditMode ? (
              <label className="modal-input modal-input--checkbox">
                <input
                  type="checkbox"
                  checked={ncfDialog.skipNcf}
                  onChange={(event) =>
                    setNcfDialog((current) => ({ ...current, skipNcf: event.target.checked, error: null }))
                  }
                  disabled={ncfDialog.isLoading}
                />
                {translate('pdf.ncfSkipLabel')}
              </label>
            ) : null}
            <label className="modal-input" aria-disabled={ncfDialog.skipNcf || undefined}>
              {translate('invoiceForm.ncfCategoryLabel')}
              <select
                value={ncfDialog.category}
                onChange={(event) =>
                  setNcfDialog((current) => {
                    const nextCategory =
                      normalizeNcfCategory(event.target.value, ncfCategories) || DEFAULT_NCF_CATEGORY
                    const nextSequenceLength = getNcfSequenceLength(nextCategory, ncfCategories)
                    return {
                      ...current,
                      category: nextCategory,
                      suffix: current.suffix.slice(0, nextSequenceLength),
                    }
                  })
                }
                disabled={ncfDialog.isLoading || ncfDialog.skipNcf}
              >
                {ncfCategories.map((option) => (
                  <option key={option.code} value={option.code}>
                    {option.code} - {(option.name || '').trim() || translate(`ncfCategories.${option.code}`)}
                  </option>
                ))}
              </select>
            </label>
            <p className="input-hint">{translate('invoiceForm.ncfCategoryHint')}</p>
            <label className="modal-input" aria-disabled={ncfDialog.skipNcf || undefined}>
              {translate('documentList.ncfLabel')}
              <div className="ncf-input-group">
                <span className="ncf-input-group__prefix">{ncfDialog.category}</span>
                <input
                  type="text"
                  value={ncfDialog.suffix}
                  onChange={(event) =>
                    setNcfDialog((current) => ({
                      ...current,
                      suffix: sanitizeNcfSuffix(event.target.value).slice(
                        0,
                        getNcfSequenceLength(current.category, ncfCategories),
                      ),
                    }))
                  }
                  placeholder={ncfDialogSuffixPlaceholder}
                  maxLength={ncfDialogSequenceLength}
                  disabled={ncfDialog.isLoading || ncfDialog.skipNcf}
                />
              </div>
            </label>
            <p className="input-hint">{translate('invoiceForm.ncfSuffixHint')}</p>
            <div className="form-actions">
              <button
                type="button"
                className="button button--ghost"
                onClick={handleCancelNcfDialog}
                disabled={ncfDialog.isLoading}
              >
                {translate('pdf.ncfDialogCancel')}
              </button>
              <button
                type="button"
                className="button"
                onClick={handleConfirmNcfDialog}
                disabled={ncfDialog.isLoading || (!ncfDialog.skipNcf && !ncfDialog.suffix.trim())}
              >
                {ncfDialog.isLoading
                  ? translate(isNcfEditMode ? 'documentList.editingNcf' : 'documentList.generatingInvoice')
                  : translate(isNcfEditMode ? 'pdf.ncfDialogConfirmEdit' : 'pdf.ncfDialogConfirm')}
              </button>
            </div>
          </div>
        </Modal>
      ) : null}
    </div>
  )
}

export default App
