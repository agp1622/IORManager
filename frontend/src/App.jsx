import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { useAuth, apiFetch } from './contexts/AuthContext'
import InvoiceForm from './components/InvoiceForm'
import Insights from './components/Insights'
import ReceiptForm from './components/ReceiptForm'
import PurchaseOrderForm from './components/PurchaseOrderForm'
import AccountPayableForm from './components/AccountPayableForm'
import Modal from './components/Modal'
import ThemedSelect from './components/ThemedSelect'
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
  purchaseOrders: 'Órdenes de compra con trazabilidad y documentación completa.',
  accountsPayable: 'Control de facturas a proveedores pendientes de pago.',
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
  accountsPayable: {
    title: t('sections.accountsPayable.title'),
    description: t('sections.accountsPayable.description'),
    endpoint: 'AccountsPayable',
    singular: t('sections.accountsPayable.singular'),
    partyLabel: t('sections.accountsPayable.partyLabel'),
    loading: t('sections.accountsPayable.loading'),
    empty: t('sections.accountsPayable.empty'),
    loadError: t('sections.accountsPayable.loadError'),
    createHeading: t('sections.accountsPayable.createHeading'),
    createDescription: t('sections.accountsPayable.createDescription'),
    createSuccess: t('sections.accountsPayable.createSuccess'),
    createError: t('sections.accountsPayable.createError'),
  },
  ncfSequences: {
    title: 'Secuencias NCF',
    // No endpoint — data comes from fiscalRegimes loaded separately
  },
  insights: {
    title: t('sections.insights.title'),
    description: t('sections.insights.description'),
    // No endpoint — data comes from the dedicated /Insights endpoint, fetched by the Insights component itself.
  },
  alerts: {
    title: t('sections.alerts.title'),
    description: t('sections.alerts.description'),
    // No endpoint — data comes from the /Alerts endpoint, already polled into the `alerts` state above.
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
  selectable = false,
  selectedIds = null,
  onToggleSelect,
  onToggleSelectAll,
}) => {
  const isQuoteList = config?.endpoint === 'Quotes'
  const isInvoiceList = config?.endpoint === 'Invoices'
  const isAccountsPayableList = config?.endpoint === 'AccountsPayable'
  const isPurchaseOrderList = config?.endpoint === 'PurchaseOrders'
  const [openActionsId, setOpenActionsId] = useState(null)
  const [menuPosition, setMenuPosition] = useState(null)
  const actionButtonRefs = useRef({})

  // The actions menu is rendered in a portal (see below) so it can escape the
  // table wrapper's `overflow-x: auto`, which otherwise clips it whenever a
  // row is near the edge of the scrollable area. Position is computed from
  // the trigger button's on-screen coordinates and kept in `position: fixed`
  // coordinates, which are viewport-relative and unaffected by ancestor overflow.
  const updateMenuPosition = useCallback((documentId) => {
    const button = actionButtonRefs.current[documentId]
    if (!button) {
      return
    }

    const rect = button.getBoundingClientRect()
    const estimatedMenuHeight = 260
    const estimatedMenuWidth = 220
    const viewportMargin = 8

    const openUpward = rect.bottom + estimatedMenuHeight > window.innerHeight && rect.top > estimatedMenuHeight

    // Prefer opening rightward (menu's left edge flush with the button's left
    // edge) — that's the natural direction and the only one that works when
    // the button sits near the left side of its row, e.g. the mobile card
    // layout where the actions button is left-aligned. Only fall back to
    // opening leftward (menu's right edge flush with the button) when there
    // isn't enough room to the right, such as a right-aligned desktop table
    // column near the edge of the screen.
    const openRightward = rect.left + estimatedMenuWidth + viewportMargin <= window.innerWidth

    setMenuPosition({
      top: openUpward ? rect.top : rect.bottom,
      openUpward,
      openRightward,
      left: openRightward ? Math.max(viewportMargin, rect.left) : undefined,
      right: openRightward ? undefined : Math.max(viewportMargin, window.innerWidth - rect.right),
    })
  }, [])

  const toggleActionsMenu = useCallback(
    (documentId) => {
      setOpenActionsId((current) => {
        const next = current === documentId ? null : documentId
        if (next) {
          updateMenuPosition(next)
        } else {
          setMenuPosition(null)
        }
        return next
      })
    },
    [updateMenuPosition],
  )

  const closeActionsMenu = useCallback(() => {
    setOpenActionsId(null)
    setMenuPosition(null)
  }, [])

  useEffect(() => {
    if (!openActionsId) {
      return undefined
    }

    const handleMouseDown = (event) => {
      const target = event.target
      if (!(target instanceof Element)) {
        return
      }

      if (target.closest('.document-table__actions') || target.closest('.document-actions-menu')) {
        return
      }

      closeActionsMenu()
    }

    const handleKeyDown = (event) => {
      if (event.key === 'Escape') {
        closeActionsMenu()
      }
    }

    // Scrolling or resizing invalidates the computed fixed position — simplest
    // and safest is to just close the menu rather than track every scroll
    // container the row might live inside.
    const handleScrollOrResize = () => {
      closeActionsMenu()
    }

    document.addEventListener('mousedown', handleMouseDown)
    document.addEventListener('keydown', handleKeyDown)
    window.addEventListener('scroll', handleScrollOrResize, true)
    window.addEventListener('resize', handleScrollOrResize)
    return () => {
      document.removeEventListener('mousedown', handleMouseDown)
      document.removeEventListener('keydown', handleKeyDown)
      window.removeEventListener('scroll', handleScrollOrResize, true)
      window.removeEventListener('resize', handleScrollOrResize)
    }
  }, [openActionsId, closeActionsMenu])

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
            {selectable ? (
              <th scope="col" className="document-table__select-col">
                <input
                  type="checkbox"
                  aria-label={t('documentList.selectAll')}
                  checked={documents.length > 0 && documents.every((doc) => selectedIds?.has(doc.id))}
                  ref={(node) => {
                    if (node) {
                      const someSelected = documents.some((doc) => selectedIds?.has(doc.id))
                      const allSelected = documents.length > 0 && documents.every((doc) => selectedIds?.has(doc.id))
                      node.indeterminate = someSelected && !allSelected
                    }
                  }}
                  onChange={(event) => onToggleSelectAll?.(documents.map((doc) => doc.id), event.target.checked)}
                />
              </th>
            ) : null}
            <th scope="col" aria-sort={getAriaSort('number')}>
              {renderSortButton(t('documentList.number'), 'number')}
            </th>
            {isInvoiceList ? (
              <th scope="col" aria-sort={getAriaSort('quoteNumber')}>
                {renderSortButton(t('documentList.quoteNumber'), 'quoteNumber')}
              </th>
            ) : null}
            {isAccountsPayableList ? (
              <th scope="col" aria-sort={getAriaSort('invoiceNumber')}>
                {renderSortButton(t('documentList.invoiceNumber'), 'invoiceNumber')}
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
            {isQuoteList ? (
              <th scope="col">{t('documentList.profitLabel')}</th>
            ) : null}
            <th scope="col" aria-sort={getAriaSort('currency')}>
              {renderSortButton(t('documentList.currency'), 'currency')}
            </th>
            {!isAccountsPayableList ? (
              <th scope="col" aria-sort={getAriaSort('ncf')}>
                {renderSortButton(t('documentList.ncfLabel'), 'ncf')}
              </th>
            ) : null}
            {isAccountsPayableList ? (
              <th scope="col" aria-sort={getAriaSort('dueDate')}>
                {renderSortButton(t('documentList.dueDate'), 'dueDate')}
              </th>
            ) : null}
            {isAccountsPayableList ? (
              <th scope="col" aria-sort={getAriaSort('status')}>
                {renderSortButton(t('documentList.status'), 'status')}
              </th>
            ) : null}
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

            const isPaid = Boolean(document.paidAt || document.isPaid)
            const isSent = Boolean(document.sentAt || document.isSent)
            const paymentDueDate = document.paymentDueDate ? new Date(document.paymentDueDate) : null
            const isPaymentDue =
              Boolean(document.isPaymentDue) ||
              (!isPaid && isSent && paymentDueDate && paymentDueDate.getTime() <= Date.now())
            const invoicePillState = isPaid ? 'paid' : isPaymentDue ? 'paymentDue' : isSent ? 'sent' : 'unpaid'
            const invoicePillLabel = {
              paid: t('documentList.paidLabel'),
              paymentDue: t('documentList.paymentDueLabel'),
              sent: t('documentList.sentLabel'),
              unpaid: t('documentList.unpaidLabel'),
            }[invoicePillState]
            const invoicePillClass = {
              paid: 'pill--success',
              paymentDue: 'pill--error',
              sent: 'pill--info',
              unpaid: 'pill--warning',
            }[invoicePillState]
            const poStatus = document.status || 'Pendiente'
            const poStatusClass =
              poStatus === 'Completada' ? 'pill--success' : poStatus === 'EnProceso' ? 'pill--info' : 'pill--warning'
            const poStatusLabel = t(`purchaseOrderForm.status${poStatus}`)

            return (
              <tr key={document.id}>
                {selectable ? (
                  <td className="document-table__select-col" data-heading={t('documentList.selectRow')}>
                    <input
                      type="checkbox"
                      aria-label={t('documentList.selectRow')}
                      checked={Boolean(selectedIds?.has(document.id))}
                      onChange={() => onToggleSelect?.(document.id)}
                    />
                  </td>
                ) : null}
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
                    {isInvoiceList ? (
                      <span className={`pill ${invoicePillClass}`}>{invoicePillLabel}</span>
                    ) : null}
                    {isPurchaseOrderList ? (
                      <span className={`pill ${poStatusClass}`}>{poStatusLabel}</span>
                    ) : null}
                  </div>
                </td>
                {isInvoiceList ? (
                  <td data-heading={t('documentList.quoteNumber')}>
                    {quoteNumber}
                  </td>
                ) : null}
                {isAccountsPayableList ? (
                  <td data-heading={t('documentList.invoiceNumber')}>
                    {document.invoice?.number || '—'}
                  </td>
                ) : null}
                <td data-heading={t('documentList.date')}>
                  {formatDate(document.date, cultureName)}
                </td>
                <td data-heading={config.partyLabel}>{partyName}</td>
                <td data-heading={t('documentList.total')}>
                  {formatCurrency(document.totalAmount, currencyCode, cultureName)}
                </td>
                {isQuoteList ? (() => {
                  const hasExpenses = Number(document.totalExpenses) > 0
                  if (!hasExpenses) {
                    return (
                      <td data-heading={t('documentList.profitLabel')} className="profit-cell profit-cell--none">
                        {t('documentList.profitNoExpenses')}
                      </td>
                    )
                  }
                  const profit = Number(document.totalAmount) - Number(document.totalExpenses)
                  const isPositive = profit >= 0
                  return (
                    <td data-heading={t('documentList.profitLabel')} className={`profit-cell ${isPositive ? 'profit-cell--positive' : 'profit-cell--negative'}`}>
                      <span className="profit-cell__amount">
                        {formatCurrency(profit, currencyCode, cultureName)}
                      </span>
                      <span className="profit-cell__expenses">
                        {t('documentList.expensesLabel')}: {formatCurrency(document.totalExpenses, currencyCode, cultureName)}
                      </span>
                    </td>
                  )
                })() : null}
                <td
                  data-heading={t('documentList.currency')}
                  className="document-table__cell--currency"
                >
                  {currencyCode || '—'}
                </td>
                {!isAccountsPayableList ? (
                  <td data-heading={t('documentList.ncfLabel')}>
                    {ncfNumber || '—'}
                  </td>
                ) : null}
                {isAccountsPayableList ? (
                  <td data-heading={t('documentList.dueDate')}>
                    {formatDate(document.dueDate, cultureName)}
                  </td>
                ) : null}
                {isAccountsPayableList ? (
                  <td data-heading={t('documentList.status')}>
                    <span className={`pill ${
                      document.status === 'Pagado' ? 'pill--success' :
                      document.status === 'Vencido' ? 'pill--error' :
                      'pill--warning'
                    }`}>
                      {document.status || '—'}
                    </span>
                  </td>
                ) : null}
                <td data-heading={t('documentList.actions')} className="document-table__actions">
                  <button
                    ref={(node) => {
                      if (node) {
                        actionButtonRefs.current[document.id] = node
                      } else {
                        delete actionButtonRefs.current[document.id]
                      }
                    }}
                    type="button"
                    className="button button--secondary document-actions-toggle"
                    onClick={() => toggleActionsMenu(document.id)}
                    aria-haspopup="menu"
                    aria-expanded={openActionsId === document.id}
                    aria-label={t('documentList.actions')}
                    title={t('documentList.actions')}
                  >
                    <span aria-hidden="true">⋯</span>
                  </button>

                  {openActionsId === document.id && menuPosition
                    ? createPortal(
                        <div
                          className={`document-actions-menu ${menuPosition.openUpward ? 'document-actions-menu--upward' : ''}`}
                          role="menu"
                          aria-label={t('documentList.actions')}
                          style={{
                            position: 'fixed',
                            ...(menuPosition.openRightward
                              ? { left: menuPosition.left }
                              : { right: menuPosition.right }),
                            ...(menuPosition.openUpward
                              ? { bottom: window.innerHeight - menuPosition.top }
                              : { top: menuPosition.top }),
                          }}
                        >
                          <button
                            type="button"
                            className="document-actions-menu__item"
                            onClick={() => {
                              closeActionsMenu()
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
                                  closeActionsMenu()
                                  action.onClick(document)
                                }}
                                disabled={isBusy}
                              >
                                {isBusy ? loadingLabel : actionLabel}
                              </button>
                            )
                          })}
                        </div>,
                        window.document.body,
                      )
                    : null}
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
    purchaseOrders: [],
    accountsPayable: [],
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
  const [fiscalRegimes, setFiscalRegimes] = useState([])
  const [customers, setCustomers] = useState([])
  const [sortConfig, setSortConfig] = useState({ key: 'date', direction: 'desc' })
  const [editingQuote, setEditingQuote] = useState(null)
  const [isQuoteModalOpen, setIsQuoteModalOpen] = useState(false)
  const [isInvoiceModalOpen, setIsInvoiceModalOpen] = useState(false)
  const [editingPO, setEditingPO] = useState(null)
  const [prefillPOQuoteId, setPrefillPOQuoteId] = useState(null)
  const [editingAP, setEditingAP] = useState(null)
  const [pageBySection, setPageBySection] = useState({ quotes: 1, invoices: 1, receipts: 1, purchaseOrders: 1, accountsPayable: 1 })
  const [viewingTrash, setViewingTrash] = useState(false)
  const [trashDocuments, setTrashDocuments] = useState({ quotes: [], invoices: [] })
  const [deletingId, setDeletingId] = useState(null)
  const [restoringId, setRestoringId] = useState(null)
  const [markingPaidId, setMarkingPaidId] = useState(null)
  const [markingUnpaidId, setMarkingUnpaidId] = useState(null)
  const [markingSentId, setMarkingSentId] = useState(null)
  const [markingUnsentId, setMarkingUnsentId] = useState(null)
  const [markingPoStatusId, setMarkingPoStatusId] = useState(null)
  const [selectedInvoiceIds, setSelectedInvoiceIds] = useState(() => new Set())
  const [isDownloadingSelectedPdf, setIsDownloadingSelectedPdf] = useState(false)
  const [isDownloadingSelectedZip, setIsDownloadingSelectedZip] = useState(false)
  const [paidFilter, setPaidFilter] = useState('all')
  const [alerts, setAlerts] = useState([])
  const [isAlertsBellOpen, setIsAlertsBellOpen] = useState(false)
  const [attachmentsDialog, setAttachmentsDialog] = useState({
    isOpen: false,
    documentId: null,
    documentNumber: null,
    attachments: [],
    isLoading: false,
    isUploading: false,
    deletingId: null,
    error: null,
  })
  const [quoteAttachmentsDialog, setQuoteAttachmentsDialog] = useState({
    isOpen: false,
    documentId: null,
    documentNumber: null,
    attachments: [],
    isLoading: false,
    isUploading: false,
    deletingId: null,
    error: null,
  })
  const previewUrlRef = useRef(null)
  const [previewingId, setPreviewingId] = useState(null)
  const alertsBellRef = useRef(null)
  const [alertsMenuPosition, setAlertsMenuPosition] = useState(null)
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
  const [editSequenceDialog, setEditSequenceDialog] = useState({
    isOpen: false,
    categoryCode: '',
    categoryName: '',
    inputValue: '',
    isLoading: false,
    error: null,
  })
  const [paymentTermsDialog, setPaymentTermsDialog] = useState({
    isOpen: false,
    customerId: null,
    customerName: '',
    inputValue: '30',
    isLoading: false,
    error: null,
  })
  const [recalculateDialog, setRecalculateDialog] = useState({
    isOpen: false,
    isLoading: false,
    error: null,
  })

  const { user, logout } = useAuth()
  const canDelete = user?.role === 'Admin'
  const canCreateInvoice = user?.role === 'Admin' || user?.role === 'Manager'

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
      if (!config?.endpoint) {
        return
      }

      setLoading(true)
      setError(null)

      try {
        const response = await apiFetch(`${API_BASE_URL}/${config.endpoint}`)
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

  const loadTrash = useCallback(
    async (sectionKey) => {
      const config = sectionConfig[sectionKey]
      if (!config?.endpoint) {
        return
      }

      setLoading(true)
      setError(null)

      try {
        const response = await apiFetch(`${API_BASE_URL}/${config.endpoint}/trash`)
        if (!response.ok) {
          throw new Error(config.loadError)
        }

        const data = await response.json()
        setTrashDocuments((current) => ({ ...current, [sectionKey]: data }))
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
      const response = await apiFetch(`${API_BASE_URL}/Customers`)
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
          defaultPaymentTermsDays: typeof customer?.defaultPaymentTermsDays === 'number' ? customer.defaultPaymentTermsDays : 30,
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

  const loadFiscalRegimes = useCallback(async () => {
    try {
      const response = await apiFetch(`${API_BASE_URL}/Invoices/fiscal-regimes`)
      if (!response.ok) return
      const data = await response.json()
      if (Array.isArray(data)) setFiscalRegimes(data)
    } catch {
      // Optional — modal still works without it
    }
  }, [])

  const loadAlerts = useCallback(async () => {
    try {
      const response = await apiFetch(`${API_BASE_URL}/Alerts`)
      if (!response.ok) return
      const data = await response.json()
      if (Array.isArray(data)) setAlerts(data)
    } catch {
      // Alerts are a convenience layer — keep the rest of the app working without them.
    }
  }, [])

  useEffect(() => {
    loadCustomers()
  }, [loadCustomers])

  useEffect(() => {
    loadAlerts()
    const intervalId = window.setInterval(loadAlerts, 60000)
    return () => window.clearInterval(intervalId)
  }, [loadAlerts])

  const toggleAlertsBell = useCallback(() => {
    setIsAlertsBellOpen((current) => {
      const next = !current
      if (next) {
        const button = alertsBellRef.current
        if (button) {
          const rect = button.getBoundingClientRect()
          const viewportMargin = 8
          setAlertsMenuPosition({
            top: rect.bottom,
            right: Math.max(viewportMargin, window.innerWidth - rect.right),
          })
        }
      }
      return next
    })
  }, [])

  useEffect(() => {
    if (!isAlertsBellOpen) {
      return undefined
    }

    const handleMouseDown = (event) => {
      const target = event.target
      if (!(target instanceof Element)) {
        return
      }
      if (target.closest('.alerts-bell') || target.closest('.alerts-bell-menu')) {
        return
      }
      setIsAlertsBellOpen(false)
    }

    const handleKeyDown = (event) => {
      if (event.key === 'Escape') {
        setIsAlertsBellOpen(false)
      }
    }

    window.document.addEventListener('mousedown', handleMouseDown)
    window.document.addEventListener('keydown', handleKeyDown)
    return () => {
      window.document.removeEventListener('mousedown', handleMouseDown)
      window.document.removeEventListener('keydown', handleKeyDown)
    }
  }, [isAlertsBellOpen])

  useEffect(() => {
    loadFiscalRegimes()
  }, [loadFiscalRegimes])

  useEffect(() => {
    let isMounted = true

    const loadNcfCategories = async () => {
      try {
        const response = await apiFetch(`${API_BASE_URL}/Invoices/ncf-categories`)
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
    setSelectedInvoiceIds(new Set())
    setPaidFilter('all')
  }, [activeSection, viewingTrash])

  useEffect(() => {
    if (activeSection !== 'quotes') {
      setEditingQuote(null)
      setIsQuoteModalOpen(false)
    }
  }, [activeSection])

  const supportsTrash = activeSection === 'quotes' || activeSection === 'invoices'

  useEffect(() => {
    if (!supportsTrash) {
      setViewingTrash(false)
    }
  }, [supportsTrash])

  useEffect(() => {
    if (viewingTrash && supportsTrash) {
      loadTrash(activeSection)
    }
  }, [viewingTrash, supportsTrash, activeSection, loadTrash])

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
        const response = await apiFetch(`${API_BASE_URL}/${config.endpoint}`, {
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
        if ((sectionKey === 'quotes' || sectionKey === 'invoices') && responseBody?.pdfBase64) {
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
    const source = (viewingTrash ? trashDocuments[activeSection] : documents[activeSection]) ?? []
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

    const appliesPaidFilter = activeSection === 'invoices' && !viewingTrash && paidFilter !== 'all'
    const paidFiltered = appliesPaidFilter
      ? source.filter((document) => {
          const isPaid = Boolean(document.paidAt || document.isPaid)
          return paidFilter === 'paid' ? isPaid : !isPaid
        })
      : source

    if (!searchTerm && !clientTerm && !numberTerm && !dateTerm && !ncfTerm) {
      return paidFiltered
    }

    return paidFiltered.filter((document) => {
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
  }, [documents, trashDocuments, viewingTrash, activeSection, filters, paidFilter])

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
  const isPurchaseOrdersSection = activeSection === 'purchaseOrders'
  const isAccountsPayableSection = activeSection === 'accountsPayable'
  const isNcfSequencesSection = activeSection === 'ncfSequences'
  const isInsightsSection = activeSection === 'insights'
  const isAlertsSection = activeSection === 'alerts'
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
        const response = await apiFetch(
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
        const response = await apiFetch(
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
        const response = await apiFetch(`${API_BASE_URL}/${config.endpoint}/${quoteId}`, {
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
        const response = await apiFetch(`${API_BASE_URL}/Quotes/${doc.id}/duplicate`, {
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

  // ── Delete / restore (soft-delete with 1-year recovery window) ────────────

  const handleDeleteDocument = useCallback(
    async (doc) => {
      const section = sectionConfig[activeSection]
      if (!section || !doc?.id) {
        return
      }

      if (typeof window !== 'undefined' && !window.confirm(translate('documentList.deleteConfirm'))) {
        return
      }

      try {
        setDeletingId(doc.id)
        const response = await apiFetch(`${API_BASE_URL}/${section.endpoint}/${doc.id}`, {
          method: 'DELETE',
        })

        if (!response.ok && response.status !== 204) {
          let detail = translate('documentList.deleteError')
          try {
            const body = await response.json()
            detail = body?.title || body?.detail || detail
          } catch {
            // Ignore parse errors and use fallback text.
          }
          throw new Error(detail)
        }

        setStatus({ type: 'success', message: translate('documentList.deleteSuccess') })
        await loadSection(activeSection)
      } catch (error) {
        setStatus({
          type: 'error',
          message: error.message || translate('documentList.deleteError'),
        })
      } finally {
        setDeletingId(null)
      }
    },
    [activeSection, loadSection, sectionConfig, translate],
  )

  const handleRestoreDocument = useCallback(
    async (doc) => {
      const section = sectionConfig[activeSection]
      if (!section || !doc?.id) {
        return
      }

      try {
        setRestoringId(doc.id)
        const response = await apiFetch(`${API_BASE_URL}/${section.endpoint}/${doc.id}/restore`, {
          method: 'POST',
        })

        const body = await response.json().catch(() => null)
        if (!response.ok) {
          const detail = body?.title || body?.detail || translate('documentList.restoreError')
          throw new Error(detail)
        }

        setStatus({ type: 'success', message: translate('documentList.restoreSuccess') })
        await Promise.all([loadTrash(activeSection), loadSection(activeSection)])
      } catch (error) {
        setStatus({
          type: 'error',
          message: error.message || translate('documentList.restoreError'),
        })
      } finally {
        setRestoringId(null)
      }
    },
    [activeSection, loadSection, loadTrash, sectionConfig, translate],
  )

  const handleToggleTrash = useCallback(() => {
    setViewingTrash((current) => !current)
    setStatus(null)
  }, [])

  // ── Paid / Unpaid status ────────────────────────────────────────────────

  const handleMarkInvoicePaid = useCallback(
    async (doc) => {
      if (!doc?.id) {
        return
      }

      try {
        setMarkingPaidId(doc.id)
        const response = await apiFetch(`${API_BASE_URL}/Invoices/${doc.id}/mark-paid`, {
          method: 'POST',
        })

        if (!response.ok) {
          let detail = translate('documentList.markPaidError')
          try {
            const body = await response.json()
            detail = body?.title || body?.detail || detail
          } catch {
            // Ignore parse errors and use fallback text.
          }
          throw new Error(detail)
        }

        await loadSection(activeSection)
      } catch (error) {
        setStatus({
          type: 'error',
          message: error.message || translate('documentList.markPaidError'),
        })
      } finally {
        setMarkingPaidId(null)
      }
    },
    [activeSection, loadSection, translate],
  )

  const handleMarkInvoiceUnpaid = useCallback(
    async (doc) => {
      if (!doc?.id) {
        return
      }

      try {
        setMarkingUnpaidId(doc.id)
        const response = await apiFetch(`${API_BASE_URL}/Invoices/${doc.id}/mark-unpaid`, {
          method: 'POST',
        })

        if (!response.ok) {
          let detail = translate('documentList.markUnpaidError')
          try {
            const body = await response.json()
            detail = body?.title || body?.detail || detail
          } catch {
            // Ignore parse errors and use fallback text.
          }
          throw new Error(detail)
        }

        await loadSection(activeSection)
      } catch (error) {
        setStatus({
          type: 'error',
          message: error.message || translate('documentList.markUnpaidError'),
        })
      } finally {
        setMarkingUnpaidId(null)
      }
    },
    [activeSection, loadSection, translate],
  )

  const handlePaidFilterChange = useCallback((value) => {
    setPaidFilter(value)
  }, [])

  const handleGoToAlert = useCallback((alert) => {
    const targetSection =
      alert.documentType === 'Quote'
        ? 'quotes'
        : alert.documentType === 'Invoice'
          ? 'invoices'
          : alert.documentType === 'PurchaseOrder'
            ? 'purchaseOrders'
            : null

    if (!targetSection) {
      return
    }

    setActiveSection(targetSection)
    setFilters((current) => ({ ...current, search: alert.documentNumber || '' }))
    setIsAlertsBellOpen(false)
  }, [])

  const handleMarkInvoiceSent = useCallback(
    async (doc) => {
      if (!doc?.id) {
        return
      }

      try {
        setMarkingSentId(doc.id)
        const response = await apiFetch(`${API_BASE_URL}/Invoices/${doc.id}/mark-sent`, {
          method: 'POST',
        })

        if (!response.ok) {
          let detail = translate('documentList.markSentError')
          try {
            const body = await response.json()
            detail = body?.title || body?.detail || detail
          } catch {
            // Ignore parse errors and use fallback text.
          }
          throw new Error(detail)
        }

        await loadSection(activeSection)
      } catch (error) {
        setStatus({
          type: 'error',
          message: error.message || translate('documentList.markSentError'),
        })
      } finally {
        setMarkingSentId(null)
      }
    },
    [activeSection, loadSection, translate],
  )

  const handleMarkInvoiceUnsent = useCallback(
    async (doc) => {
      if (!doc?.id) {
        return
      }

      try {
        setMarkingUnsentId(doc.id)
        const response = await apiFetch(`${API_BASE_URL}/Invoices/${doc.id}/mark-unsent`, {
          method: 'POST',
        })

        if (!response.ok) {
          let detail = translate('documentList.markUnsentError')
          try {
            const body = await response.json()
            detail = body?.title || body?.detail || detail
          } catch {
            // Ignore parse errors and use fallback text.
          }
          throw new Error(detail)
        }

        await loadSection(activeSection)
      } catch (error) {
        setStatus({
          type: 'error',
          message: error.message || translate('documentList.markUnsentError'),
        })
      } finally {
        setMarkingUnsentId(null)
      }
    },
    [activeSection, loadSection, translate],
  )

  const handleUpdatePurchaseOrderStatus = useCallback(
    async (doc, nextStatus) => {
      if (!doc?.id) {
        return
      }

      try {
        setMarkingPoStatusId(doc.id)
        const response = await apiFetch(`${API_BASE_URL}/PurchaseOrders/${doc.id}/status`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ status: nextStatus }),
        })

        if (!response.ok) {
          let detail = translate('purchaseOrderForm.markStatusError')
          try {
            const body = await response.json()
            detail = body?.title || body?.detail || detail
          } catch {
            // Ignore parse errors and use fallback text.
          }
          throw new Error(detail)
        }

        await loadSection(activeSection)
        await loadAlerts()
      } catch (error) {
        setStatus({
          type: 'error',
          message: error.message || translate('purchaseOrderForm.markStatusError'),
        })
      } finally {
        setMarkingPoStatusId(null)
      }
    },
    [activeSection, loadAlerts, loadSection, translate],
  )

  // ── Bulk selection + batch download (invoices only) ─────────────────────

  const handleToggleSelectInvoice = useCallback((documentId) => {
    setSelectedInvoiceIds((current) => {
      const next = new Set(current)
      if (next.has(documentId)) {
        next.delete(documentId)
      } else {
        next.add(documentId)
      }
      return next
    })
  }, [])

  const handleToggleSelectAllInvoices = useCallback((ids, shouldSelect) => {
    setSelectedInvoiceIds((current) => {
      const next = new Set(current)
      ids.forEach((id) => {
        if (shouldSelect) {
          next.add(id)
        } else {
          next.delete(id)
        }
      })
      return next
    })
  }, [])

  const downloadBlobResponse = useCallback(async (response, fallbackFileName) => {
    const disposition = response.headers.get('content-disposition') || ''
    const match = disposition.match(/filename\*?=(?:UTF-8'')?"?([^";]+)"?/i)
    const fileName = match ? decodeURIComponent(match[1]) : fallbackFileName

    const blob = await response.blob()
    const url = window.URL.createObjectURL(blob)
    const link = window.document.createElement('a')
    link.href = url
    link.download = fileName
    window.document.body.appendChild(link)
    link.click()
    window.document.body.removeChild(link)
    window.URL.revokeObjectURL(url)
  }, [])

  const handleDownloadSelectedPdf = useCallback(async () => {
    const ids = Array.from(selectedInvoiceIds)
    if (ids.length === 0) {
      return
    }

    try {
      setIsDownloadingSelectedPdf(true)
      const response = await apiFetch(`${API_BASE_URL}/Invoices/batch-pdf`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ids }),
      })

      if (!response.ok) {
        throw new Error(translate('documentList.bulkDownloadError'))
      }

      await downloadBlobResponse(response, 'invoices.pdf')
    } catch (error) {
      setStatus({
        type: 'error',
        message: error.message || translate('documentList.bulkDownloadError'),
      })
    } finally {
      setIsDownloadingSelectedPdf(false)
    }
  }, [downloadBlobResponse, selectedInvoiceIds, translate])

  const handleDownloadSelectedZip = useCallback(async () => {
    const ids = Array.from(selectedInvoiceIds)
    if (ids.length === 0) {
      return
    }

    try {
      setIsDownloadingSelectedZip(true)
      const response = await apiFetch(`${API_BASE_URL}/Invoices/batch-zip`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ ids }),
      })

      if (!response.ok) {
        throw new Error(translate('documentList.bulkDownloadError'))
      }

      await downloadBlobResponse(response, 'invoices.zip')
    } catch (error) {
      setStatus({
        type: 'error',
        message: error.message || translate('documentList.bulkDownloadError'),
      })
    } finally {
      setIsDownloadingSelectedZip(false)
    }
  }, [downloadBlobResponse, selectedInvoiceIds, translate])

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

  // ── Standalone invoice creation (no quote required) ────────────────────────

  const handleOpenInvoiceModal = useCallback(() => {
    setStatus(null)
    setIsInvoiceModalOpen(true)
  }, [])

  const handleCloseInvoiceModal = useCallback(() => {
    setIsInvoiceModalOpen(false)
  }, [])

  const handleInvoiceSubmit = useCallback(
    async (payload) => {
      const created = await handleCreate('invoices', payload)
      if (created) {
        setIsInvoiceModalOpen(false)
      }
      return created
    },
    [handleCreate],
  )

  const handleEditPO = useCallback((doc) => {
    setEditingPO(doc)
    setStatus(null)
    setActiveSection('purchaseOrders')
  }, [])

  const handleCancelEditPO = useCallback(() => {
    setEditingPO(null)
    setPrefillPOQuoteId(null)
    setStatus(null)
  }, [])

  const handleLogExpense = useCallback((doc) => {
    setEditingPO(null)
    setPrefillPOQuoteId(doc.id)
    setStatus(null)
    setActiveSection('purchaseOrders')
  }, [])

  const handleUpdatePO = useCallback(
    async (poId, payload) => {
      setIsSubmitting(true)
      setStatus(null)
      try {
        const response = await apiFetch(`${API_BASE_URL}/PurchaseOrders/${poId}`, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(payload),
        })
        if (!response.ok) {
          let detail = translate('purchaseOrderForm.updateError')
          try {
            const body = await response.json()
            detail = body?.title || body?.detail || detail
          } catch { /* ignore */ }
          throw new Error(detail)
        }
        setStatus({ type: 'success', message: translate('purchaseOrderForm.updateSuccess') })
        await loadSection('purchaseOrders')
        return true
      } catch (error) {
        setStatus({ type: 'error', message: error.message || translate('purchaseOrderForm.updateError') })
        return false
      } finally {
        setIsSubmitting(false)
      }
    },
    [loadSection, translate],
  )

  const handlePOSubmit = useCallback(
    async (payload) => {
      if (editingPO) {
        const wasSuccessful = await handleUpdatePO(editingPO.id, payload)
        if (wasSuccessful) setEditingPO(null)
        return wasSuccessful
      }
      return handleCreate('purchaseOrders', payload)
    },
    [editingPO, handleCreate, handleUpdatePO],
  )

  // ── Accounts Payable handlers ───────────────────────────────────────────────

  const handleEditAP = useCallback((doc) => {
    setEditingAP(doc)
    setStatus(null)
    setActiveSection('accountsPayable')
  }, [])

  const handleCancelEditAP = useCallback(() => {
    setEditingAP(null)
    setStatus(null)
  }, [])

  const handleUpdateAP = useCallback(
    async (apId, payload) => {
      setIsSubmitting(true)
      setStatus(null)
      try {
        const response = await apiFetch(`${API_BASE_URL}/AccountsPayable/${apId}`, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(payload),
        })
        if (!response.ok) {
          let detail = translate('sections.accountsPayable.updateError')
          try {
            const body = await response.json()
            detail = body?.title || body?.detail || detail
          } catch { /* ignore */ }
          throw new Error(detail)
        }
        setStatus({ type: 'success', message: translate('sections.accountsPayable.updateSuccess') })
        await loadSection('accountsPayable')
        return true
      } catch (error) {
        setStatus({ type: 'error', message: error.message || translate('sections.accountsPayable.updateError') })
        return false
      } finally {
        setIsSubmitting(false)
      }
    },
    [loadSection, translate],
  )

  const handleAPSubmit = useCallback(
    async (payload) => {
      if (editingAP) {
        const wasSuccessful = await handleUpdateAP(editingAP.id, payload)
        if (wasSuccessful) setEditingAP(null)
        return wasSuccessful
      }
      return handleCreate('accountsPayable', payload)
    },
    [editingAP, handleCreate, handleUpdateAP],
  )

  const handleMarkAPPaid = useCallback(
    async (doc) => {
      if (!doc) return
      const payload = {
        supplierName: doc.supplierName || doc.partyName || '',
        customerPO: doc.customerPO || null,
        amount: doc.totalAmount || 0,
        currencyCode: doc.currencyCode || 'USD',
        date: typeof doc.date === 'string' ? doc.date.slice(0, 10) : new Date(doc.date).toISOString().slice(0, 10),
        dueDate: typeof doc.dueDate === 'string' ? doc.dueDate.slice(0, 10) : new Date(doc.dueDate).toISOString().slice(0, 10),
        status: 'Pagado',
        notes: doc.notes || null,
      }
      await handleUpdateAP(doc.id, payload)
    },
    [handleUpdateAP],
  )

  // ── Customer payment terms handlers ────────────────────────────────────────

  const handleOpenPaymentTermsDialog = useCallback((customer) => {
    setPaymentTermsDialog({
      isOpen: true,
      customerId: customer.id,
      customerName: customer.name,
      inputValue: String(customer.defaultPaymentTermsDays ?? 30),
      isLoading: false,
      error: null,
    })
  }, [])

  const handleClosePaymentTermsDialog = useCallback(() => {
    setPaymentTermsDialog((current) => ({ ...current, isOpen: false }))
  }, [])

  const handleSavePaymentTerms = useCallback(async () => {
    const days = parseInt(paymentTermsDialog.inputValue, 10)
    if (Number.isNaN(days) || days < 1 || days > 365) {
      setPaymentTermsDialog((current) => ({
        ...current,
        error: translate('paymentTerms.daysInputHint'),
      }))
      return
    }

    setPaymentTermsDialog((current) => ({ ...current, isLoading: true, error: null }))
    try {
      const response = await apiFetch(
        `${API_BASE_URL}/Customers/${paymentTermsDialog.customerId}/payment-terms`,
        {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ defaultPaymentTermsDays: days }),
        },
      )
      if (!response.ok) {
        const body = await response.json().catch(() => null)
        throw new Error(body?.detail || body?.title || translate('paymentTerms.saveError'))
      }
      setPaymentTermsDialog((current) => ({ ...current, isOpen: false, isLoading: false }))
      setStatus({ type: 'success', message: translate('paymentTerms.saveSuccess') })
      await loadCustomers()
    } catch (err) {
      setPaymentTermsDialog((current) => ({
        ...current,
        isLoading: false,
        error: err.message || translate('paymentTerms.saveError'),
      }))
    }
  }, [loadCustomers, paymentTermsDialog.customerId, paymentTermsDialog.inputValue, translate])

  // ── Recalculate due dates handler ──────────────────────────────────────────

  const handleRecalculateDueDates = useCallback(async () => {
    setRecalculateDialog((current) => ({ ...current, isLoading: true, error: null }))
    try {
      const response = await apiFetch(`${API_BASE_URL}/AccountsPayable/recalculate-due-dates`, {
        method: 'POST',
      })
      const body = await response.json().catch(() => null)
      if (!response.ok) {
        throw new Error(body?.detail || body?.title || translate('paymentTerms.recalculateError'))
      }
      const count = body?.updatedCount ?? 0
      setRecalculateDialog({ isOpen: false, isLoading: false, error: null })
      setStatus({
        type: 'success',
        message: count === 0
          ? translate('paymentTerms.recalculateNone')
          : translate('paymentTerms.recalculateSuccess').replace('{{count}}', String(count)),
      })
      await loadSection('accountsPayable')
    } catch (err) {
      setRecalculateDialog((current) => ({
        ...current,
        isLoading: false,
        error: err.message || translate('paymentTerms.recalculateError'),
      }))
    }
  }, [loadSection, translate])

  const downloadInvoicePdfById = useCallback(
    async (invoiceId, fileNameSeed) => {
      const response = await apiFetch(`${API_BASE_URL}/Invoices/${invoiceId}/pdf`)
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

      const response = await apiFetch(`${API_BASE_URL}/Quotes/${quoteId}/convert`, {
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

      const response = await apiFetch(`${API_BASE_URL}/Invoices/${invoiceId}/ncf`, {
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
      const response = await apiFetch(`${API_BASE_URL}/Quotes/${quoteId}/undo-conversion`, {
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
      const response = await apiFetch(`${API_BASE_URL}/Invoices/${invoiceId}`)
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
    const response = await apiFetch(
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
      await Promise.all([loadSection('quotes'), loadSection('invoices'), loadFiscalRegimes()])
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
    loadFiscalRegimes,
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

  const handleOpenAttachmentsDialog = useCallback(async (doc) => {
    if (!doc?.id) return

    setAttachmentsDialog({
      isOpen: true,
      documentId: doc.id,
      documentNumber: doc.number || doc.id,
      attachments: [],
      isLoading: true,
      isUploading: false,
      deletingId: null,
      error: null,
    })

    try {
      const response = await apiFetch(`${API_BASE_URL}/PurchaseOrders/${doc.id}/attachments`)
      if (!response.ok) throw new Error(translate('purchaseOrderForm.attachmentsLoading'))
      const data = await response.json()
      setAttachmentsDialog((current) => ({
        ...current,
        attachments: Array.isArray(data) ? data : [],
        isLoading: false,
      }))
    } catch {
      setAttachmentsDialog((current) => ({
        ...current,
        isLoading: false,
        error: translate('purchaseOrderForm.attachUploadError'),
      }))
    }
  }, [translate])

  const handleCloseAttachmentsDialog = useCallback(() => {
    setAttachmentsDialog({
      isOpen: false,
      documentId: null,
      documentNumber: null,
      attachments: [],
      isLoading: false,
      isUploading: false,
      deletingId: null,
      error: null,
    })
  }, [])

  const handleUploadAttachment = useCallback(async (file) => {
    const { documentId } = attachmentsDialog
    if (!documentId || !file) return

    setAttachmentsDialog((current) => ({ ...current, isUploading: true, error: null }))

    try {
      const formData = new FormData()
      formData.append('file', file)

      const response = await apiFetch(`${API_BASE_URL}/PurchaseOrders/${documentId}/attachments`, {
        method: 'POST',
        body: formData,
      })

      if (!response.ok) {
        const body = await response.json().catch(() => null)
        throw new Error(body?.detail || body?.title || translate('purchaseOrderForm.attachUploadError'))
      }

      const newAttachment = await response.json()
      setAttachmentsDialog((current) => ({
        ...current,
        attachments: [...current.attachments, newAttachment],
        isUploading: false,
      }))
    } catch (err) {
      setAttachmentsDialog((current) => ({
        ...current,
        isUploading: false,
        error: err.message || translate('purchaseOrderForm.attachUploadError'),
      }))
    }
  }, [attachmentsDialog, translate])

  const handleDeleteAttachment = useCallback(async (attachmentId) => {
    const { documentId } = attachmentsDialog
    if (!documentId) return

    setAttachmentsDialog((current) => ({ ...current, deletingId: attachmentId, error: null }))

    try {
      const response = await apiFetch(
        `${API_BASE_URL}/PurchaseOrders/${documentId}/attachments/${attachmentId}`,
        { method: 'DELETE' },
      )

      if (!response.ok) throw new Error(translate('purchaseOrderForm.attachDeleteError'))

      setAttachmentsDialog((current) => ({
        ...current,
        attachments: current.attachments.filter((a) => a.id !== attachmentId),
        deletingId: null,
      }))
    } catch (err) {
      setAttachmentsDialog((current) => ({
        ...current,
        deletingId: null,
        error: err.message || translate('purchaseOrderForm.attachDeleteError'),
      }))
    }
  }, [attachmentsDialog, translate])

  const handleDownloadAttachment = useCallback(async (attachment) => {
    const { documentId } = attachmentsDialog
    if (!documentId) return

    try {
      const response = await apiFetch(
        `${API_BASE_URL}/PurchaseOrders/${documentId}/attachments/${attachment.id}/download`,
      )
      if (!response.ok) throw new Error(translate('pdf.downloadError'))

      const blob = await response.blob()
      const url = window.URL.createObjectURL(blob)
      const link = window.document.createElement('a')
      link.href = url
      link.download = attachment.fileName || `attachment-${attachment.id}`
      window.document.body.appendChild(link)
      link.click()
      window.document.body.removeChild(link)
      window.URL.revokeObjectURL(url)
    } catch {
      setAttachmentsDialog((current) => ({
        ...current,
        error: translate('pdf.downloadError'),
      }))
    }
  }, [attachmentsDialog, translate])

  const handleOpenQuoteAttachmentsDialog = useCallback(async (doc) => {
    if (!doc?.id) return

    setQuoteAttachmentsDialog({
      isOpen: true,
      documentId: doc.id,
      documentNumber: doc.number || doc.id,
      attachments: [],
      isLoading: true,
      isUploading: false,
      deletingId: null,
      error: null,
    })

    try {
      const response = await apiFetch(`${API_BASE_URL}/Quotes/${doc.id}/attachments`)
      if (!response.ok) throw new Error(translate('quoteAttachments.attachmentsLoading'))
      const data = await response.json()
      setQuoteAttachmentsDialog((current) => ({
        ...current,
        attachments: Array.isArray(data) ? data : [],
        isLoading: false,
      }))
    } catch {
      setQuoteAttachmentsDialog((current) => ({
        ...current,
        isLoading: false,
        error: translate('quoteAttachments.attachUploadError'),
      }))
    }
  }, [translate])

  const handleCloseQuoteAttachmentsDialog = useCallback(() => {
    setQuoteAttachmentsDialog({
      isOpen: false,
      documentId: null,
      documentNumber: null,
      attachments: [],
      isLoading: false,
      isUploading: false,
      deletingId: null,
      error: null,
    })
  }, [])

  const handleUploadQuoteAttachment = useCallback(async (file) => {
    const { documentId } = quoteAttachmentsDialog
    if (!documentId || !file) return

    setQuoteAttachmentsDialog((current) => ({ ...current, isUploading: true, error: null }))

    try {
      const formData = new FormData()
      formData.append('file', file)

      const response = await apiFetch(`${API_BASE_URL}/Quotes/${documentId}/attachments`, {
        method: 'POST',
        body: formData,
      })

      if (!response.ok) {
        const body = await response.json().catch(() => null)
        throw new Error(body?.detail || body?.title || translate('quoteAttachments.attachUploadError'))
      }

      const newAttachment = await response.json()
      setQuoteAttachmentsDialog((current) => ({
        ...current,
        attachments: [...current.attachments, newAttachment],
        isUploading: false,
      }))
    } catch (err) {
      setQuoteAttachmentsDialog((current) => ({
        ...current,
        isUploading: false,
        error: err.message || translate('quoteAttachments.attachUploadError'),
      }))
    }
  }, [quoteAttachmentsDialog, translate])

  const handleDeleteQuoteAttachment = useCallback(async (attachmentId) => {
    const { documentId } = quoteAttachmentsDialog
    if (!documentId) return

    setQuoteAttachmentsDialog((current) => ({ ...current, deletingId: attachmentId, error: null }))

    try {
      const response = await apiFetch(
        `${API_BASE_URL}/Quotes/${documentId}/attachments/${attachmentId}`,
        { method: 'DELETE' },
      )

      if (!response.ok) throw new Error(translate('quoteAttachments.attachDeleteError'))

      setQuoteAttachmentsDialog((current) => ({
        ...current,
        attachments: current.attachments.filter((a) => a.id !== attachmentId),
        deletingId: null,
      }))
    } catch (err) {
      setQuoteAttachmentsDialog((current) => ({
        ...current,
        deletingId: null,
        error: err.message || translate('quoteAttachments.attachDeleteError'),
      }))
    }
  }, [quoteAttachmentsDialog, translate])

  const handleDownloadQuoteAttachment = useCallback(async (attachment) => {
    const { documentId } = quoteAttachmentsDialog
    if (!documentId) return

    try {
      const response = await apiFetch(
        `${API_BASE_URL}/Quotes/${documentId}/attachments/${attachment.id}/download`,
      )
      if (!response.ok) throw new Error(translate('pdf.downloadError'))

      const blob = await response.blob()
      const url = window.URL.createObjectURL(blob)
      const link = window.document.createElement('a')
      link.href = url
      link.download = attachment.fileName || `attachment-${attachment.id}`
      window.document.body.appendChild(link)
      link.click()
      window.document.body.removeChild(link)
      window.URL.revokeObjectURL(url)
    } catch {
      setQuoteAttachmentsDialog((current) => ({
        ...current,
        error: translate('pdf.downloadError'),
      }))
    }
  }, [quoteAttachmentsDialog, translate])

  const documentExtraActions = useMemo(() => {
    const previewAction = {
      label: translate('documentList.preview'),
      loadingLabel: translate('documentList.previewing'),
      busyId: previewingId,
      onClick: handlePreviewDocument,
      variant: 'button--ghost',
    }

    if (viewingTrash && (isQuotesSection || isInvoicesSection)) {
      return [
        {
          label: translate('documentList.restore'),
          loadingLabel: translate('documentList.restoring'),
          busyId: restoringId,
          onClick: handleRestoreDocument,
          variant: 'button--ghost',
        },
      ]
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
          label: translate('quoteAttachments.attachmentsAction'),
          loadingLabel: translate('quoteAttachments.attachmentsLoading'),
          busyId: null,
          onClick: handleOpenQuoteAttachmentsDialog,
          variant: 'button--ghost',
        },
        {
          label: translate('documentList.duplicateQuote'),
          loadingLabel: translate('documentList.duplicatingQuote'),
          busyId: duplicatingQuoteId,
          onClick: handleDuplicateQuote,
          variant: 'button--ghost',
        },
        ...(canCreateInvoice ? [{
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
        }] : []),
        ...(canCreateInvoice ? [{
          label: translate('documentList.editNcf'),
          loadingLabel: translate('documentList.editingNcf'),
          busyId: updatingNcfId,
          isVisible: (doc) => Boolean(doc?.convertedInvoiceId || doc?.convertedAt),
          onClick: (doc) => {
            void handleOpenNcfDialog(doc, 'edit')
          },
          variant: 'button--ghost',
        }] : []),
        ...(canDelete ? [{
          label: translate('documentList.undoInvoice'),
          loadingLabel: translate('documentList.undoingInvoice'),
          busyId: undoingQuoteId,
          isVisible: (doc) => Boolean(doc?.convertedInvoiceId || doc?.convertedAt),
          onClick: handleUndoConvertedInvoice,
          variant: 'button--ghost',
        }] : []),
        {
          label: translate('purchaseOrderForm.logExpenseAction'),
          loadingLabel: translate('purchaseOrderForm.logExpenseAction'),
          busyId: null,
          onClick: handleLogExpense,
          variant: 'button--ghost',
        },
        ...(canDelete ? [{
          label: translate('documentList.delete'),
          loadingLabel: translate('documentList.deleting'),
          busyId: deletingId,
          onClick: handleDeleteDocument,
          variant: 'button--ghost',
        }] : []),
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
        {
          label: translate('documentList.markPaid'),
          loadingLabel: translate('documentList.markingPaid'),
          busyId: markingPaidId,
          isVisible: (doc) => !(doc?.paidAt || doc?.isPaid),
          onClick: handleMarkInvoicePaid,
          variant: 'button--ghost',
        },
        {
          label: translate('documentList.markUnpaid'),
          loadingLabel: translate('documentList.markingUnpaid'),
          busyId: markingUnpaidId,
          isVisible: (doc) => Boolean(doc?.paidAt || doc?.isPaid),
          onClick: handleMarkInvoiceUnpaid,
          variant: 'button--ghost',
        },
        {
          label: translate('documentList.markSent'),
          loadingLabel: translate('documentList.markingSent'),
          busyId: markingSentId,
          isVisible: (doc) => !(doc?.sentAt || doc?.isSent),
          onClick: handleMarkInvoiceSent,
          variant: 'button--ghost',
        },
        {
          label: translate('documentList.markUnsent'),
          loadingLabel: translate('documentList.markingUnsent'),
          busyId: markingUnsentId,
          isVisible: (doc) => Boolean(doc?.sentAt || doc?.isSent),
          onClick: handleMarkInvoiceUnsent,
          variant: 'button--ghost',
        },
        ...(canDelete ? [{
          label: translate('documentList.delete'),
          loadingLabel: translate('documentList.deleting'),
          busyId: deletingId,
          onClick: handleDeleteDocument,
          variant: 'button--ghost',
        }] : []),
      ]
    }

    if (isPurchaseOrdersSection) {
      return [
        previewAction,
        {
          label: translate('purchaseOrderForm.attachmentsAction'),
          loadingLabel: translate('purchaseOrderForm.attachmentsLoading'),
          busyId: null,
          onClick: handleOpenAttachmentsDialog,
          variant: 'button--ghost',
        },
        {
          label: translate('purchaseOrderForm.editAction'),
          loadingLabel: translate('purchaseOrderForm.editAction'),
          busyId: null,
          onClick: handleEditPO,
          variant: 'button--ghost',
        },
        {
          label: translate('purchaseOrderForm.markInProgressAction'),
          loadingLabel: translate('purchaseOrderForm.markInProgressLoading'),
          busyId: markingPoStatusId,
          isVisible: (doc) => (doc?.status || 'Pendiente') === 'Pendiente',
          onClick: (doc) => handleUpdatePurchaseOrderStatus(doc, 'EnProceso'),
          variant: 'button--ghost',
        },
        {
          label: translate('purchaseOrderForm.markCompletedAction'),
          loadingLabel: translate('purchaseOrderForm.markCompletedLoading'),
          busyId: markingPoStatusId,
          isVisible: (doc) => (doc?.status || 'Pendiente') !== 'Completada',
          onClick: (doc) => handleUpdatePurchaseOrderStatus(doc, 'Completada'),
          variant: 'button--ghost',
        },
      ]
    }

    if (isAccountsPayableSection) {
      return [
        {
          label: translate('accountPayableForm.editAction'),
          loadingLabel: translate('accountPayableForm.editAction'),
          busyId: editingAP?.id ?? null,
          onClick: handleEditAP,
          variant: 'button--ghost',
        },
        {
          label: translate('accountPayableForm.markPaidAction'),
          loadingLabel: translate('accountPayableForm.markPaidLoading'),
          busyId: null,
          isVisible: (doc) => doc?.status !== 'Pagado',
          onClick: handleMarkAPPaid,
          variant: 'button--ghost',
        },
      ]
    }

    return [previewAction]
  }, [
    canCreateInvoice,
    canDelete,
    deletingId,
    duplicatingQuoteId,
    editingAP?.id,
    editingQuote?.id,
    handleDeleteDocument,
    handleDuplicateQuote,
    handleEditAP,
    handleEditPO,
    handleEditQuote,
    handleGenerateInvoice,
    handleLogExpense,
    handleMarkAPPaid,
    handleMarkInvoicePaid,
    handleMarkInvoiceUnpaid,
    handleMarkInvoiceSent,
    handleMarkInvoiceUnsent,
    handleUpdatePurchaseOrderStatus,
    handleOpenAttachmentsDialog,
    handleOpenNcfDialog,
    handleOpenQuoteAttachmentsDialog,
    handlePreviewDocument,
    handleRestoreDocument,
    handleUndoConvertedInvoice,
    invoiceGeneratingId,
    isAccountsPayableSection,
    isInvoicesSection,
    isPurchaseOrdersSection,
    isQuotesSection,
    markingPaidId,
    markingUnpaidId,
    markingSentId,
    markingUnsentId,
    markingPoStatusId,
    previewingId,
    restoringId,
    translate,
    undoingQuoteId,
    updatingNcfId,
    viewingTrash,
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

  // Last NCF used for whichever category is currently selected in the dialog
  const lastNcfForCurrentCategory = useMemo(() => {
    if (!ncfDialog.isOpen) return null
    const regime = fiscalRegimes.find((r) => r.code === ncfDialog.category)
    return regime?.lastNcfUsed ?? null
  }, [fiscalRegimes, ncfDialog.isOpen, ncfDialog.category])

  // ── NCF Sequences tab handlers ──────────────────────────────────────────────

  const handleOpenEditSequence = useCallback((regime) => {
    // Extract the pure numeric part from the next NCF string (e.g. "B02" + "00000046" → 46)
    const rawSuffix = regime.nextNcf ? regime.nextNcf.slice(regime.code.length) : '1'
    const nextNumber = parseInt(rawSuffix, 10) || 1
    setEditSequenceDialog({
      isOpen: true,
      categoryCode: regime.code,
      categoryName: regime.name,
      inputValue: String(nextNumber),
      isLoading: false,
      error: null,
    })
  }, [])

  const handleSaveSequence = useCallback(async () => {
    const nextNumber = parseInt(editSequenceDialog.inputValue, 10)
    if (Number.isNaN(nextNumber) || nextNumber < 1) {
      setEditSequenceDialog((current) => ({
        ...current,
        error: 'El número debe ser al menos 1.',
      }))
      return
    }
    setEditSequenceDialog((current) => ({ ...current, isLoading: true, error: null }))
    try {
      const response = await apiFetch(
        `${API_BASE_URL}/Invoices/ncf-sequences/${editSequenceDialog.categoryCode}`,
        {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ nextNumber }),
        },
      )
      if (!response.ok) {
        const body = await response.json().catch(() => null)
        throw new Error(body?.detail || body?.title || 'Error al actualizar la secuencia.')
      }
      setEditSequenceDialog((current) => ({ ...current, isOpen: false }))
      await loadFiscalRegimes()
    } catch (err) {
      setEditSequenceDialog((current) => ({
        ...current,
        isLoading: false,
        error: err.message || 'Error al guardar.',
      }))
    }
  }, [editSequenceDialog.categoryCode, editSequenceDialog.inputValue, loadFiscalRegimes])

  // When the user picks a different category, update the dialog and fetch the
  // suggested next NCF for that category so the suffix auto-fills.
  const handleNcfCategoryChange = useCallback(
    async (newCategory) => {
      setNcfDialog((current) => {
        if (!current.isOpen) return current
        const nextSequenceLength = getNcfSequenceLength(newCategory, ncfCategories)
        return {
          ...current,
          category: newCategory,
          suffix: current.suffix.slice(0, nextSequenceLength),
        }
      })

      if (isNcfEditMode) return

      try {
        const suggested = await fetchSuggestedNcf(newCategory)
        setNcfDialog((current) => {
          if (!current.isOpen || current.category !== newCategory) return current
          return {
            ...current,
            suffix: extractNcfSuffix(suggested.ncfNumber, newCategory),
          }
        })
      } catch {
        // Ignore — leave suffix as-is if the fetch fails
      }
    },
    [extractNcfSuffix, fetchSuggestedNcf, isNcfEditMode, ncfCategories],
  )

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
          <div className="alerts-bell">
            <button
              type="button"
              ref={alertsBellRef}
              className="alerts-bell__trigger"
              onClick={toggleAlertsBell}
              aria-haspopup="menu"
              aria-expanded={isAlertsBellOpen}
              aria-label={translate('alerts.bellLabel')}
              title={translate('alerts.bellLabel')}
            >
              <UiIcon className="ui-icon">
                <path d="M10 3.5a4 4 0 0 0-4 4v2.2c0 .6-.2 1.2-.6 1.7l-1 1.3a1 1 0 0 0 .8 1.6h9.6a1 1 0 0 0 .8-1.6l-1-1.3a2.8 2.8 0 0 1-.6-1.7V7.5a4 4 0 0 0-4-4z" />
                <path d="M8.3 15.5a1.7 1.7 0 0 0 3.4 0" />
              </UiIcon>
              {alerts.length > 0 ? (
                <span className="alerts-bell__badge">{alerts.length > 9 ? '9+' : alerts.length}</span>
              ) : null}
            </button>

            {isAlertsBellOpen && alertsMenuPosition
              ? createPortal(
                  <div
                    className="alerts-bell-menu"
                    role="menu"
                    aria-label={translate('alerts.bellLabel')}
                    style={{
                      position: 'fixed',
                      top: alertsMenuPosition.top,
                      right: alertsMenuPosition.right,
                    }}
                  >
                    {alerts.length === 0 ? (
                      <p className="alerts-bell-menu__empty">{translate('alerts.empty')}</p>
                    ) : (
                      alerts.map((alert) => (
                        <button
                          key={alert.id}
                          type="button"
                          className="alerts-bell-menu__item"
                          onClick={() => handleGoToAlert(alert)}
                        >
                          <span className={`pill ${alert.severity === 'error' ? 'pill--error' : 'pill--warning'}`}>
                            {alert.type === 'SendInvoice' ? translate('alerts.typeSendInvoice') : translate('alerts.typePaymentDue')}
                          </span>
                          <span>{alert.message}</span>
                        </button>
                      ))
                    )}
                    <button
                      type="button"
                      className="alerts-bell-menu__viewAll"
                      onClick={() => {
                        setActiveSection('alerts')
                        setIsAlertsBellOpen(false)
                      }}
                    >
                      {translate('alerts.viewAll')}
                    </button>
                  </div>,
                  window.document.body,
                )
              : null}
          </div>
          {user ? (
            <div className="top-nav__user">
              <span className="top-nav__user-name">{user.name}</span>
              <span className="pill pill--muted">{user.role}</span>
              <button
                type="button"
                className="button button--ghost button--compact"
                onClick={logout}
              >
                Cerrar sesión
              </button>
            </div>
          ) : null}
          <label className="toolbar__control">
            {translate('controls.language')}
            <ThemedSelect
              value={language}
              onChange={setLanguage}
              ariaLabel={translate('controls.language')}
              options={LANGUAGES.map((entry) => ({ value: entry.value, label: entry.label }))}
            />
          </label>
          <label className="toolbar__control">
            {translate('controls.currency')}
            <ThemedSelect
              value={defaultCurrency}
              onChange={setDefaultCurrency}
              ariaLabel={translate('controls.currency')}
              options={CURRENCY_CODES.map((code) => ({ value: code, label: translate(`currencies.${code}`) }))}
            />
          </label>
          <label className="toolbar__control">
            {translate('controls.theme')}
            <ThemedSelect
              value={theme}
              onChange={setTheme}
              ariaLabel={translate('controls.theme')}
              options={[
                { value: 'light', label: translate('controls.themeLight') },
                { value: 'dark', label: translate('controls.themeDark') },
              ]}
            />
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

      {isNcfSequencesSection ? (
        <section className="section-content">
          <div className="section-intro">
            <h2>Secuencias NCF</h2>
            <p>Último NCF emitido y próximo a asignar por cada categoría fiscal.</p>
          </div>
          <div className="document-table-wrapper">
            <table className="document-table">
              <thead>
                <tr>
                  <th>Categoría</th>
                  <th>Nombre</th>
                  <th style={{ textAlign: 'right' }}>Facturas</th>
                  <th>Último NCF</th>
                  <th>Próximo NCF</th>
                  <th style={{ textAlign: 'right' }}>Acción</th>
                </tr>
              </thead>
              <tbody>
                {fiscalRegimes.length === 0 ? (
                  <tr>
                    <td colSpan={6} style={{ textAlign: 'center', opacity: 0.6 }}>
                      Cargando secuencias…
                    </td>
                  </tr>
                ) : (
                  fiscalRegimes.map((regime) => (
                    <tr key={regime.code}>
                      <td>
                        <code style={{ fontWeight: 600 }}>{regime.code}</code>
                      </td>
                      <td>{regime.name}</td>
                      <td style={{ textAlign: 'right' }}>{regime.invoiceCount ?? 0}</td>
                      <td>{regime.lastNcfUsed ?? <span style={{ opacity: 0.45 }}>—</span>}</td>
                      <td>
                        <span style={{ fontVariantNumeric: 'tabular-nums' }}>{regime.nextNcf}</span>
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        <button
                          type="button"
                          className="button button--ghost button--compact"
                          onClick={() => handleOpenEditSequence(regime)}
                        >
                          Editar
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </section>
      ) : null}

      {isInsightsSection ? (
        <section className="section-content">
          <div className="section-intro">
            <h2>{config.title}</h2>
            <p>{config.description}</p>
          </div>
          <Insights
            apiBaseUrl={API_BASE_URL}
            currency={defaultCurrency}
            currencyOptions={CURRENCY_CODES}
            onCurrencyChange={setDefaultCurrency}
            t={translate}
            formatCurrency={formatCurrency}
            locale={locale}
          />
        </section>
      ) : null}

      {isAlertsSection ? (
        <section className="section-content">
          <div className="section-intro">
            <h2>{config.title}</h2>
            <p>{config.description}</p>
          </div>
          {alerts.length === 0 ? (
            <p className="muted">{translate('alerts.empty')}</p>
          ) : (
            <ul className="alerts-list">
              {alerts.map((alert) => (
                <li key={alert.id} className={`alerts-list__item alerts-list__item--${alert.severity}`}>
                  <div className="alerts-list__copy">
                    <span className={`pill ${alert.severity === 'error' ? 'pill--error' : 'pill--warning'}`}>
                      {alert.type === 'SendInvoice' ? translate('alerts.typeSendInvoice') : translate('alerts.typePaymentDue')}
                    </span>
                    <p>{alert.message}</p>
                  </div>
                  <button
                    type="button"
                    className="button button--secondary button--compact"
                    onClick={() => handleGoToAlert(alert)}
                  >
                    {translate('alerts.goToDocument')}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>
      ) : null}

      {!isNcfSequencesSection && !isInsightsSection && !isAlertsSection ? (
      <section className="section-content">
        <div className={`section-intro ${(isQuotesSection || isInvoicesSection) ? 'section-intro--with-action' : ''}`}>
          <div className="section-intro__copy">
            <h2>{config.title}</h2>
            <p>{config.description}</p>
          </div>
          <div className="section-intro__actions">
            {isQuotesSection && !viewingTrash ? (
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
            {isInvoicesSection && !viewingTrash ? (
              <button
                type="button"
                className="button button--compact button--icon-text"
                onClick={handleOpenInvoiceModal}
              >
                <UiIcon className="ui-icon ui-icon--button">
                  <path d="M10 4v12" />
                  <path d="M4 10h12" />
                </UiIcon>
                {translate('sections.invoices.newInvoice')}
              </button>
            ) : null}
            {canDelete && supportsTrash ? (
              <button
                type="button"
                className="button button--secondary button--compact button--icon-text"
                onClick={handleToggleTrash}
              >
                <UiIcon className="ui-icon ui-icon--button">
                  <path d="M4 6h12" />
                  <path d="M6 6V4.8A1.2 1.2 0 0 1 7.2 3.6h5.6A1.2 1.2 0 0 1 14 4.8V6" />
                  <path d="M6 6v9.2A1.2 1.2 0 0 0 7.2 16.4h5.6A1.2 1.2 0 0 0 14 15.2V6" />
                </UiIcon>
                {viewingTrash ? translate('documentList.viewActive') : translate('documentList.viewTrash')}
              </button>
            ) : null}
          </div>
        </div>

        {viewingTrash ? (
          <p className="muted trash-hint">{translate('documentList.trashHint')}</p>
        ) : null}

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
            {isInvoicesSection && !viewingTrash ? (
              <label>
                <span className="field-label">
                  <UiIcon>
                    <circle cx="10" cy="10" r="7" />
                    <path d="M7 10.5l2 2 4-4.5" />
                  </UiIcon>
                  <span>{translate('documentList.paidStatusLabel')}</span>
                </span>
                <ThemedSelect
                  value={paidFilter}
                  onChange={handlePaidFilterChange}
                  ariaLabel={translate('documentList.paidStatusLabel')}
                  options={[
                    { value: 'all', label: translate('documentList.paidStatusAll') },
                    { value: 'paid', label: translate('documentList.paidStatusPaid') },
                    { value: 'unpaid', label: translate('documentList.paidStatusUnpaid') },
                  ]}
                />
              </label>
            ) : null}
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

        {isInvoicesSection && !viewingTrash && selectedInvoiceIds.size > 0 ? (
          <div className="bulk-actions">
            <span className="bulk-actions__count">
              {translate('documentList.selectedCount').replace('{{count}}', selectedInvoiceIds.size)}
            </span>
            <button
              type="button"
              className="button button--secondary button--compact"
              onClick={handleDownloadSelectedPdf}
              disabled={isDownloadingSelectedPdf}
            >
              {isDownloadingSelectedPdf
                ? translate('documentList.downloadingSelectedPdf')
                : translate('documentList.downloadSelectedPdf')}
            </button>
            <button
              type="button"
              className="button button--secondary button--compact"
              onClick={handleDownloadSelectedZip}
              disabled={isDownloadingSelectedZip}
            >
              {isDownloadingSelectedZip
                ? translate('documentList.downloadingSelectedZip')
                : translate('documentList.downloadSelectedZip')}
            </button>
          </div>
        ) : null}

        {loading ? (
          <p className="muted">{config.loading}</p>
        ) : error ? (
          <div className="banner banner--error" role="alert">
            {error}
          </div>
        ) : currentDocuments.length === 0 ? (
          <p className="muted">{viewingTrash ? translate('documentList.trashEmpty') : config.empty}</p>
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
            selectable={isInvoicesSection && !viewingTrash}
            selectedIds={selectedInvoiceIds}
            onToggleSelect={handleToggleSelectInvoice}
            onToggleSelectAll={handleToggleSelectAllInvoices}
          />
        )}
      </section>
      ) : null}

      {!isQuotesSection && !isNcfSequencesSection && !isInsightsSection && !isAlertsSection ? (
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
          ) : isPurchaseOrdersSection ? (
            <PurchaseOrderForm
              onSubmit={handlePOSubmit}
              isSubmitting={isSubmitting}
              t={translate}
              quotes={documents.quotes}
              initialPO={editingPO}
              mode={editingPO ? 'edit' : 'create'}
              onCancelEdit={handleCancelEditPO}
              prefillQuoteId={prefillPOQuoteId}
            />
          ) : isAccountsPayableSection ? (
            editingAP ? (
              <AccountPayableForm
                onSubmit={handleAPSubmit}
                isSubmitting={isSubmitting}
                t={translate}
                initialAP={editingAP}
                mode="edit"
                onCancelEdit={handleCancelEditAP}
              />
            ) : (
              <>
                <div className="quote-cta">
                  <div className="quote-cta__copy">
                    <p>{translate('sections.accountsPayable.autoHint')}</p>
                  </div>
                  <div className="quote-cta__actions">
                    <button
                      type="button"
                      className="button"
                      onClick={() => setActiveSection('quotes')}
                    >
                      {translate('sections.accountsPayable.openQuotes')}
                    </button>
                  </div>
                </div>

                <div className="section-intro section-intro--with-action" style={{ marginTop: '2rem' }}>
                  <div className="section-intro__copy">
                    <h3 style={{ margin: '0 0 0.25rem' }}>{translate('paymentTerms.heading')}</h3>
                    <p style={{ margin: 0 }}>{translate('paymentTerms.description')}</p>
                  </div>
                  <button
                    type="button"
                    className="button button--secondary button--compact"
                    onClick={() => setRecalculateDialog({ isOpen: true, isLoading: false, error: null })}
                  >
                    {translate('paymentTerms.recalculateButton')}
                  </button>
                </div>

                {customers.length === 0 ? (
                  <p className="muted">{translate('paymentTerms.noCustomers')}</p>
                ) : (
                  <div className="document-table-wrapper">
                    <table className="document-table">
                      <thead>
                        <tr>
                          <th scope="col">{translate('paymentTerms.customerLabel')}</th>
                          <th scope="col" style={{ textAlign: 'right' }}>{translate('paymentTerms.daysLabel')}</th>
                          <th scope="col" style={{ textAlign: 'right' }}>{translate('documentList.actions')}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {customers.map((customer) => (
                          <tr key={customer.id}>
                            <td>{customer.name}</td>
                            <td style={{ textAlign: 'right' }}>
                              <strong>{customer.defaultPaymentTermsDays ?? 30}</strong>
                            </td>
                            <td style={{ textAlign: 'right' }}>
                              <button
                                type="button"
                                className="button button--ghost button--compact"
                                onClick={() => handleOpenPaymentTermsDialog(customer)}
                              >
                                {translate('paymentTerms.editAction')}
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </>
            )
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
      {isInvoicesSection ? (
        <Modal
          isOpen={isInvoiceModalOpen}
          onClose={handleCloseInvoiceModal}
          title={translate('sections.invoices.createHeading')}
          description={translate('sections.invoices.createDescription')}
          eyebrow={config.title}
        >
          <InvoiceForm
            onSubmit={handleInvoiceSubmit}
            isSubmitting={isSubmitting}
            t={translate}
            defaultCurrency={defaultCurrency}
            locale={locale}
            mode="create"
            customers={customers}
          />
        </Modal>
      ) : null}
      {attachmentsDialog.isOpen ? (
        <Modal
          isOpen={attachmentsDialog.isOpen}
          onClose={attachmentsDialog.isUploading ? undefined : handleCloseAttachmentsDialog}
          title={translate('purchaseOrderForm.attachmentsHeading')}
          description={attachmentsDialog.documentNumber || ''}
          eyebrow={translate('sections.purchaseOrders.title')}
        >
          <div className="modal-body">
            {attachmentsDialog.error ? (
              <div className="banner banner--error" role="alert">
                {attachmentsDialog.error}
              </div>
            ) : null}
            {attachmentsDialog.isLoading ? (
              <p className="muted">{translate('purchaseOrderForm.attachmentsLoading')}</p>
            ) : attachmentsDialog.attachments.length === 0 ? (
              <p className="muted">{translate('purchaseOrderForm.attachmentsEmpty')}</p>
            ) : (
              <ul className="attachments-list">
                {attachmentsDialog.attachments.map((attachment) => {
                  const isDeleting = attachmentsDialog.deletingId === attachment.id
                  const fileSizeKb = Math.ceil((attachment.fileSize || 0) / 1024)
                  return (
                    <li key={attachment.id} className="attachments-list__item">
                      <span className="attachments-list__name">{attachment.fileName}</span>
                      <span className="attachments-list__meta">
                        {fileSizeKb} KB
                      </span>
                      <div className="attachments-list__actions">
                        <button
                          type="button"
                          className="button button--secondary button--compact"
                          onClick={() => handleDownloadAttachment(attachment)}
                        >
                          {translate('purchaseOrderForm.attachDownload')}
                        </button>
                        {canDelete ? (
                          <button
                            type="button"
                            className="button button--ghost button--compact"
                            onClick={() => handleDeleteAttachment(attachment.id)}
                            disabled={isDeleting}
                          >
                            {isDeleting
                              ? translate('purchaseOrderForm.attachDeleting')
                              : translate('purchaseOrderForm.attachDelete')}
                          </button>
                        ) : null}
                      </div>
                    </li>
                  )
                })}
              </ul>
            )}
            <div className="attachments-upload">
              <label className="button button--secondary" style={{ cursor: 'pointer' }}>
                {attachmentsDialog.isUploading
                  ? translate('purchaseOrderForm.attachUploading')
                  : translate('purchaseOrderForm.attachUpload')}
                <input
                  type="file"
                  style={{ display: 'none' }}
                  disabled={attachmentsDialog.isUploading || attachmentsDialog.isLoading}
                  onChange={(event) => {
                    const file = event.target.files?.[0]
                    if (file) {
                      event.target.value = ''
                      void handleUploadAttachment(file)
                    }
                  }}
                />
              </label>
            </div>
          </div>
        </Modal>
      ) : null}
      {quoteAttachmentsDialog.isOpen ? (
        <Modal
          isOpen={quoteAttachmentsDialog.isOpen}
          onClose={quoteAttachmentsDialog.isUploading ? undefined : handleCloseQuoteAttachmentsDialog}
          title={translate('quoteAttachments.attachmentsHeading')}
          description={quoteAttachmentsDialog.documentNumber || ''}
          eyebrow={translate('sections.quotes.title')}
        >
          <div className="modal-body">
            {quoteAttachmentsDialog.error ? (
              <div className="banner banner--error" role="alert">
                {quoteAttachmentsDialog.error}
              </div>
            ) : null}
            {quoteAttachmentsDialog.isLoading ? (
              <p className="muted">{translate('quoteAttachments.attachmentsLoading')}</p>
            ) : quoteAttachmentsDialog.attachments.length === 0 ? (
              <p className="muted">{translate('quoteAttachments.attachmentsEmpty')}</p>
            ) : (
              <ul className="attachments-list">
                {quoteAttachmentsDialog.attachments.map((attachment) => {
                  const isDeleting = quoteAttachmentsDialog.deletingId === attachment.id
                  const fileSizeKb = Math.ceil((attachment.fileSize || 0) / 1024)
                  return (
                    <li key={attachment.id} className="attachments-list__item">
                      <span className="attachments-list__name">{attachment.fileName}</span>
                      <span className="attachments-list__meta">
                        {fileSizeKb} KB
                      </span>
                      <div className="attachments-list__actions">
                        <button
                          type="button"
                          className="button button--secondary button--compact"
                          onClick={() => handleDownloadQuoteAttachment(attachment)}
                        >
                          {translate('quoteAttachments.attachDownload')}
                        </button>
                        {canDelete ? (
                          <button
                            type="button"
                            className="button button--ghost button--compact"
                            onClick={() => handleDeleteQuoteAttachment(attachment.id)}
                            disabled={isDeleting}
                          >
                            {isDeleting
                              ? translate('quoteAttachments.attachDeleting')
                              : translate('quoteAttachments.attachDelete')}
                          </button>
                        ) : null}
                      </div>
                    </li>
                  )
                })}
              </ul>
            )}
            <div className="attachments-upload">
              <label className="button button--secondary" style={{ cursor: 'pointer' }}>
                {quoteAttachmentsDialog.isUploading
                  ? translate('quoteAttachments.attachUploading')
                  : translate('quoteAttachments.attachUpload')}
                <input
                  type="file"
                  style={{ display: 'none' }}
                  disabled={quoteAttachmentsDialog.isUploading || quoteAttachmentsDialog.isLoading}
                  onChange={(event) => {
                    const file = event.target.files?.[0]
                    if (file) {
                      event.target.value = ''
                      void handleUploadQuoteAttachment(file)
                    }
                  }}
                />
              </label>
            </div>
          </div>
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
              <ThemedSelect
                value={ncfDialog.category}
                onChange={(nextValue) => {
                  const nextCategory =
                    normalizeNcfCategory(nextValue, ncfCategories) || DEFAULT_NCF_CATEGORY
                  void handleNcfCategoryChange(nextCategory)
                }}
                disabled={ncfDialog.isLoading || ncfDialog.skipNcf}
                ariaLabel={translate('invoiceForm.ncfCategoryLabel')}
                options={ncfCategories.map((option) => ({
                  value: option.code,
                  label: `${option.code} - ${(option.name || '').trim() || translate(`ncfCategories.${option.code}`)}`,
                }))}
              />
            </label>
            <p className="input-hint">{translate('invoiceForm.ncfCategoryHint')}</p>
            {lastNcfForCurrentCategory ? (
              <p className="input-hint input-hint--accent">
                Último usado: <strong>{lastNcfForCurrentCategory}</strong>
              </p>
            ) : null}
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

      {recalculateDialog.isOpen ? (
        <Modal
          isOpen={recalculateDialog.isOpen}
          onClose={recalculateDialog.isLoading ? undefined : () => setRecalculateDialog({ isOpen: false, isLoading: false, error: null })}
          title={translate('paymentTerms.recalculateConfirmTitle')}
          description={translate('paymentTerms.recalculateConfirmDescription')}
          eyebrow={translate('paymentTerms.heading')}
        >
          <div className="form-fields">
            {recalculateDialog.error ? (
              <p className="input-hint input-hint--error">{recalculateDialog.error}</p>
            ) : null}
            <div className="form-actions">
              <button
                type="button"
                className="button button--ghost"
                onClick={() => setRecalculateDialog({ isOpen: false, isLoading: false, error: null })}
                disabled={recalculateDialog.isLoading}
              >
                {translate('paymentTerms.recalculateCancel')}
              </button>
              <button
                type="button"
                className="button"
                onClick={handleRecalculateDueDates}
                disabled={recalculateDialog.isLoading}
              >
                {recalculateDialog.isLoading
                  ? translate('paymentTerms.recalculateRunning')
                  : translate('paymentTerms.recalculateConfirm')}
              </button>
            </div>
          </div>
        </Modal>
      ) : null}

      {paymentTermsDialog.isOpen ? (
        <Modal
          isOpen={paymentTermsDialog.isOpen}
          onClose={paymentTermsDialog.isLoading ? undefined : handleClosePaymentTermsDialog}
          title={translate('paymentTerms.editDialogTitle')}
          description={paymentTermsDialog.customerName}
          eyebrow={translate('paymentTerms.heading')}
        >
          <div className="form-fields">
            <label className="modal-input">
              {translate('paymentTerms.daysInputLabel')}
              <input
                type="number"
                min={1}
                max={365}
                step={1}
                value={paymentTermsDialog.inputValue}
                onChange={(event) =>
                  setPaymentTermsDialog((current) => ({
                    ...current,
                    inputValue: event.target.value,
                    error: null,
                  }))
                }
                disabled={paymentTermsDialog.isLoading}
                autoFocus
              />
            </label>
            <p className="input-hint">{translate('paymentTerms.daysInputHint')}</p>
            {paymentTermsDialog.error ? (
              <p className="input-hint input-hint--error">{paymentTermsDialog.error}</p>
            ) : null}
            <div className="form-actions">
              <button
                type="button"
                className="button button--ghost"
                onClick={handleClosePaymentTermsDialog}
                disabled={paymentTermsDialog.isLoading}
              >
                {translate('paymentTerms.cancelButton')}
              </button>
              <button
                type="button"
                className="button"
                onClick={handleSavePaymentTerms}
                disabled={paymentTermsDialog.isLoading || !paymentTermsDialog.inputValue.trim()}
              >
                {paymentTermsDialog.isLoading
                  ? translate('paymentTerms.saving')
                  : translate('paymentTerms.saveButton')}
              </button>
            </div>
          </div>
        </Modal>
      ) : null}

      {editSequenceDialog.isOpen ? (
        <Modal
          isOpen={editSequenceDialog.isOpen}
          onClose={() => setEditSequenceDialog((current) => ({ ...current, isOpen: false }))}
          title="Editar secuencia NCF"
          description={`Categoría ${editSequenceDialog.categoryCode} — ${editSequenceDialog.categoryName}`}
          eyebrow="Secuencias NCF"
        >
          <div className="form-fields">
            <label className="modal-input">
              Próximo número a asignar
              <input
                type="number"
                min={1}
                step={1}
                value={editSequenceDialog.inputValue}
                onChange={(event) =>
                  setEditSequenceDialog((current) => ({
                    ...current,
                    inputValue: event.target.value,
                    error: null,
                  }))
                }
                disabled={editSequenceDialog.isLoading}
                autoFocus
              />
            </label>
            <p className="input-hint">
              El próximo NCF generado automáticamente para <strong>{editSequenceDialog.categoryCode}</strong> usará
              este número como punto de partida.
            </p>
            {editSequenceDialog.error ? (
              <p className="input-hint input-hint--error">{editSequenceDialog.error}</p>
            ) : null}
            <div className="form-actions">
              <button
                type="button"
                className="button button--ghost"
                onClick={() => setEditSequenceDialog((current) => ({ ...current, isOpen: false }))}
                disabled={editSequenceDialog.isLoading}
              >
                Cancelar
              </button>
              <button
                type="button"
                className="button"
                onClick={handleSaveSequence}
                disabled={editSequenceDialog.isLoading || !editSequenceDialog.inputValue.trim()}
              >
                {editSequenceDialog.isLoading ? 'Guardando…' : 'Guardar'}
              </button>
            </div>
          </div>
        </Modal>
      ) : null}
    </div>
  )
}

export default App
