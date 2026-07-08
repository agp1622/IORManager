import { useCallback, useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'

// A fully custom-styled stand-in for <select>. Native <select> popups are
// rendered by the OS/browser, not the page, so our dark theme can only ever
// partially reach them (background sometimes stays light, the hovered/selected
// row keeps the system highlight color, etc.) — that's what made the Payment
// status filter (and the language/currency/theme/NCF selects) look washed-out
// in dark mode. This component renders its own listbox in a portal so every
// pixel of it follows our CSS variables, matching the same "escape the
// clipping/theme box" trick already used for the row-actions menu in
// DocumentList.
const ThemedSelect = ({ value, options, onChange, ariaLabel, disabled = false, id, className = '' }) => {
  const [isOpen, setIsOpen] = useState(false)
  const [menuPosition, setMenuPosition] = useState(null)
  const triggerRef = useRef(null)

  const selectedOption = options.find((option) => option.value === value) ?? options[0]

  const updatePosition = useCallback(() => {
    const trigger = triggerRef.current
    if (!trigger) {
      return
    }

    const rect = trigger.getBoundingClientRect()
    const estimatedMenuHeight = Math.min(280, options.length * 40 + 16)
    const openUpward = rect.bottom + estimatedMenuHeight > window.innerHeight && rect.top > estimatedMenuHeight

    setMenuPosition({
      left: rect.left,
      width: rect.width,
      openUpward,
      top: openUpward ? undefined : rect.bottom,
      bottom: openUpward ? window.innerHeight - rect.top : undefined,
    })
  }, [options.length])

  const openMenu = useCallback(() => {
    updatePosition()
    setIsOpen(true)
  }, [updatePosition])

  const closeMenu = useCallback(() => {
    setIsOpen(false)
    setMenuPosition(null)
  }, [])

  useEffect(() => {
    if (!isOpen) {
      return undefined
    }

    const handleMouseDown = (event) => {
      const target = event.target
      if (!(target instanceof Element)) {
        return
      }
      if (target.closest('.themed-select') || target.closest('.themed-select__menu')) {
        return
      }
      closeMenu()
    }

    const handleKeyDown = (event) => {
      if (event.key === 'Escape') {
        closeMenu()
      }
    }

    const handleScrollOrResize = (event) => {
      const target = event.target
      // Scrolling inside the menu's own listbox (e.g. to reach options below
      // the max-height cutoff) shouldn't close it — only scrolling elsewhere
      // on the page should.
      if (target instanceof Element && target.closest('.themed-select__menu')) {
        return
      }
      closeMenu()
    }

    window.document.addEventListener('mousedown', handleMouseDown)
    window.document.addEventListener('keydown', handleKeyDown)
    window.addEventListener('scroll', handleScrollOrResize, true)
    window.addEventListener('resize', handleScrollOrResize)
    return () => {
      window.document.removeEventListener('mousedown', handleMouseDown)
      window.document.removeEventListener('keydown', handleKeyDown)
      window.removeEventListener('scroll', handleScrollOrResize, true)
      window.removeEventListener('resize', handleScrollOrResize)
    }
  }, [isOpen, closeMenu])

  return (
    <div className={`themed-select ${className}`.trim()}>
      <button
        type="button"
        id={id}
        ref={triggerRef}
        className="themed-select__trigger"
        onClick={() => {
          if (disabled) return
          isOpen ? closeMenu() : openMenu()
        }}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        aria-label={ariaLabel}
        disabled={disabled}
      >
        <span>{selectedOption?.label}</span>
        <svg
          className="ui-icon themed-select__chevron"
          viewBox="0 0 20 20"
          fill="none"
          stroke="currentColor"
          strokeWidth="1.8"
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
          focusable="false"
        >
          <path d="M5.5 8l4.5 4.5L14.5 8" />
        </svg>
      </button>

      {isOpen && menuPosition && !disabled
        ? createPortal(
            <ul
              role="listbox"
              aria-label={ariaLabel}
              className={`themed-select__menu ${menuPosition.openUpward ? 'themed-select__menu--upward' : ''}`}
              style={{
                position: 'fixed',
                left: menuPosition.left,
                width: menuPosition.width,
                ...(menuPosition.openUpward
                  ? { bottom: menuPosition.bottom }
                  : { top: menuPosition.top }),
              }}
            >
              {options.map((option) => (
                <li key={option.value} role="presentation">
                  <button
                    type="button"
                    role="option"
                    aria-selected={option.value === value}
                    className={`themed-select__option ${option.value === value ? 'is-selected' : ''}`}
                    onClick={() => {
                      onChange(option.value)
                      closeMenu()
                    }}
                  >
                    {option.label}
                  </button>
                </li>
              ))}
            </ul>,
            window.document.body,
          )
      : null}
    </div>
  )
}

export default ThemedSelect
