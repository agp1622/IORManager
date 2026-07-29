import { useEffect, useId } from 'react'

const Modal = ({ isOpen, onClose, title, description, eyebrow, children, className = '' }) => {
  const titleId = useId()

  useEffect(() => {
    if (!isOpen) {
      return undefined
    }

    const handleKeyDown = (event) => {
      if (event.key === 'Escape') {
        onClose?.()
      }
    }

    const { overflow } = document.body.style
    document.body.style.overflow = 'hidden'
    document.addEventListener('keydown', handleKeyDown)

    return () => {
      document.body.style.overflow = overflow
      document.removeEventListener('keydown', handleKeyDown)
    }
  }, [isOpen, onClose])

  if (!isOpen) {
    return null
  }

  const handleOverlayClick = (event) => {
    event.stopPropagation()
    onClose?.()
  }

  const stopPropagation = (event) => event.stopPropagation()

  return (
    <div className="modal-overlay" onMouseDown={handleOverlayClick}>
      <div
        className={`modal ${className}`.trim()}
        role="dialog"
        aria-modal="true"
        aria-labelledby={`modal-title-${titleId}`}
        onMouseDown={stopPropagation}
      >
        <div className="modal__header">
          <div className="modal__titles">
            {eyebrow ? <p className="modal__eyebrow">{eyebrow}</p> : null}
            <h3 id={`modal-title-${titleId}`}>{title}</h3>
            {description ? <p className="modal__description">{description}</p> : null}
          </div>
          <button
            type="button"
            className="button button--ghost modal__close"
            onClick={onClose}
            aria-label="Close dialog"
          >
            ×
          </button>
        </div>
        <div className="modal__body">{children}</div>
      </div>
    </div>
  )
}

export default Modal
