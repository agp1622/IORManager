import { useEffect, useId, useState } from 'react'
import ThemedSelect from './ThemedSelect'

const CURRENCY_CODES = ['USD', 'DOP']
const AP_STATUSES = ['Pendiente', 'Pagado', 'Vencido']

const todayString = () => new Date().toISOString().split('T')[0]

const addDays = (dateStr, days) => {
  const d = new Date(dateStr)
  d.setDate(d.getDate() + days)
  return d.toISOString().split('T')[0]
}

const AccountPayableForm = ({
  onSubmit,
  isSubmitting,
  t = (v) => v,
  initialAP = null,
  mode = 'create',
  onCancelEdit,
}) => {
  const today = todayString()
  const formInstanceId = useId()
  const isEditMode = mode === 'edit' && initialAP

  const [number, setNumber] = useState('')
  const [supplierName, setSupplierName] = useState('')
  const [customerPO, setCustomerPO] = useState('')
  const [amount, setAmount] = useState('')
  const [currencyCode, setCurrencyCode] = useState('USD')
  const [date, setDate] = useState(today)
  const [dueDate, setDueDate] = useState(addDays(today, 30))
  const [status, setStatus] = useState('Pendiente')
  const [notes, setNotes] = useState('')

  // Populate form in edit mode
  useEffect(() => {
    if (!initialAP) {
      setNumber('')
      setSupplierName('')
      setCustomerPO('')
      setAmount('')
      setCurrencyCode('USD')
      setDate(today)
      setDueDate(addDays(today, 30))
      setStatus('Pendiente')
      setNotes('')
      return
    }

    const toDateInput = (val) => {
      if (!val) return today
      if (typeof val === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(val)) return val
      return new Date(val).toISOString().split('T')[0]
    }

    setNumber(initialAP.number || '')
    setSupplierName(initialAP.supplierName || initialAP.partyName || '')
    setCustomerPO(initialAP.customerPO || '')
    setAmount(initialAP.totalAmount != null ? String(initialAP.totalAmount) : '')
    setCurrencyCode(initialAP.currencyCode || 'USD')
    setDate(toDateInput(initialAP.date))
    setDueDate(toDateInput(initialAP.dueDate))
    setStatus(initialAP.status || 'Pendiente')
    setNotes(initialAP.notes || '')
  }, [initialAP]) // eslint-disable-line react-hooks/exhaustive-deps

  const handleSubmit = (e) => {
    e.preventDefault()

    const payload = {
      ...(isEditMode
        ? {
            // Update request fields
            supplierName: supplierName.trim(),
            customerPO: customerPO.trim() || null,
            amount: parseFloat(amount) || 0,
            currencyCode,
            date,
            dueDate,
            status,
            notes: notes.trim() || null,
          }
        : {
            // Create request fields
            number: number.trim(),
            supplierName: supplierName.trim(),
            customerPO: customerPO.trim() || null,
            amount: parseFloat(amount) || 0,
            currencyCode,
            date,
            dueDate,
            status,
            notes: notes.trim() || null,
          }),
    }

    void onSubmit(payload)
  }

  return (
    <form
      id={`${formInstanceId}-ap-form`}
      className="document-form"
      onSubmit={handleSubmit}
      noValidate
    >
      {isEditMode ? (
        <p className="document-form__editing-label">
          {t('accountPayableForm.editingLabel')}{' '}
          <strong>{initialAP.number}</strong>
        </p>
      ) : null}

      <div className="document-form__grid">
        {/* Invoice number — only on create */}
        {!isEditMode ? (
          <div className="document-form__field">
            <label className="document-form__label" htmlFor={`${formInstanceId}-number`}>
              {t('accountPayableForm.numberLabel')}
            </label>
            <input
              id={`${formInstanceId}-number`}
              type="text"
              className="document-form__input"
              value={number}
              onChange={(e) => setNumber(e.target.value)}
              placeholder={t('accountPayableForm.numberPlaceholder')}
              required
            />
          </div>
        ) : null}

        {/* Supplier */}
        <div className="document-form__field">
          <label className="document-form__label" htmlFor={`${formInstanceId}-supplier`}>
            {t('accountPayableForm.supplierLabel')}
          </label>
          <input
            id={`${formInstanceId}-supplier`}
            type="text"
            className="document-form__input"
            value={supplierName}
            onChange={(e) => setSupplierName(e.target.value)}
            placeholder={t('accountPayableForm.supplierPlaceholder')}
            required
          />
        </div>

        {/* Customer PO (optional) */}
        <div className="document-form__field">
          <label className="document-form__label" htmlFor={`${formInstanceId}-po`}>
            {t('accountPayableForm.customerPOLabel')}
          </label>
          <input
            id={`${formInstanceId}-po`}
            type="text"
            className="document-form__input"
            value={customerPO}
            onChange={(e) => setCustomerPO(e.target.value)}
            placeholder={t('accountPayableForm.customerPOPlaceholder')}
          />
        </div>

        {/* Amount */}
        <div className="document-form__field">
          <label className="document-form__label" htmlFor={`${formInstanceId}-amount`}>
            {t('accountPayableForm.amountLabel')}
          </label>
          <input
            id={`${formInstanceId}-amount`}
            type="number"
            className="document-form__input"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            min="0"
            step="0.01"
            placeholder="0.00"
            required
          />
        </div>

        {/* Currency */}
        <div className="document-form__field">
          <label className="document-form__label" htmlFor={`${formInstanceId}-currency`}>
            {t('accountPayableForm.currencyLabel')}
          </label>
          <ThemedSelect
            id={`${formInstanceId}-currency`}
            value={currencyCode}
            onChange={setCurrencyCode}
            ariaLabel={t('accountPayableForm.currencyLabel')}
            options={CURRENCY_CODES.map((code) => ({ value: code, label: code }))}
          />
        </div>

        {/* Invoice date */}
        <div className="document-form__field">
          <label className="document-form__label" htmlFor={`${formInstanceId}-date`}>
            {t('accountPayableForm.dateLabel')}
          </label>
          <input
            id={`${formInstanceId}-date`}
            type="date"
            className="document-form__input"
            value={date}
            onChange={(e) => setDate(e.target.value)}
            required
          />
        </div>

        {/* Due date */}
        <div className="document-form__field">
          <label className="document-form__label" htmlFor={`${formInstanceId}-due`}>
            {t('accountPayableForm.dueDateLabel')}
          </label>
          <input
            id={`${formInstanceId}-due`}
            type="date"
            className="document-form__input"
            value={dueDate}
            onChange={(e) => setDueDate(e.target.value)}
            required
          />
        </div>

        {/* Status */}
        <div className="document-form__field">
          <label className="document-form__label" htmlFor={`${formInstanceId}-status`}>
            {t('accountPayableForm.statusLabel')}
          </label>
          <ThemedSelect
            id={`${formInstanceId}-status`}
            value={status}
            onChange={setStatus}
            ariaLabel={t('accountPayableForm.statusLabel')}
            options={AP_STATUSES.map((s) => ({ value: s, label: t(`accountPayableForm.status${s}`) }))}
          />
        </div>
      </div>

      {/* Notes */}
      <div className="document-form__field document-form__field--full">
        <label className="document-form__label" htmlFor={`${formInstanceId}-notes`}>
          {t('accountPayableForm.notesLabel')}
        </label>
        <textarea
          id={`${formInstanceId}-notes`}
          className="document-form__input document-form__textarea"
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          placeholder={t('accountPayableForm.notesPlaceholder')}
          rows={3}
          maxLength={1000}
        />
      </div>

      {/* Actions */}
      <div className="document-form__actions">
        {isEditMode && onCancelEdit ? (
          <button
            type="button"
            className="button button--ghost"
            onClick={onCancelEdit}
            disabled={isSubmitting}
          >
            {t('accountPayableForm.cancelEdit')}
          </button>
        ) : null}
        <button
          type="submit"
          className="button"
          disabled={isSubmitting}
        >
          {isSubmitting
            ? isEditMode
              ? t('accountPayableForm.updating')
              : t('accountPayableForm.submitting')
            : isEditMode
              ? t('accountPayableForm.updateSubmit')
              : t('accountPayableForm.submit')}
        </button>
      </div>
    </form>
  )
}

export default AccountPayableForm
